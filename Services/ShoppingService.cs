using System.Text;
using Dapper;

namespace HomeAccounting.Services;

public static class ShoppingService
{
    /// <summary>Подкатегории продуктов и хозтоваров текущего пользователя с наиболее частой единицей.</summary>
    public static List<ShopItem> GetItems()
    {
        using var conn = Db.Open();
        var sql = @"
            WITH subs AS (
                SELECT sc.id, sc.name AS sname, c.name AS cname
                FROM subcategories sc
                JOIN categories c ON c.id = sc.category_id
                WHERE sc.user_id = @uid AND c.type = 'expense'
                  AND (c.name LIKE '%родукт%' OR c.name LIKE '%итани%'
                    OR c.name LIKE '%озяйствен%' OR c.name LIKE '%озтовар%')
            ),
            uc AS (
                SELECT s.id AS sid, u.name AS unit_name,
                       ROW_NUMBER() OVER (PARTITION BY s.id ORDER BY COUNT(*) DESC) AS rn
                FROM expenses e
                JOIN subs s ON e.subcategory_id = s.id
                JOIN units u ON u.id = e.unit_id
                WHERE e.user_id = @uid AND e.unit_id IS NOT NULL
                GROUP BY s.id, u.name
            )
            SELECT s.sname AS Name, s.cname AS Category, COALESCE(uc.unit_name, 'шт.') AS Unit
            FROM subs s
            LEFT JOIN uc ON uc.sid = s.id AND uc.rn = 1
            ORDER BY s.cname, s.sname";
        return conn.Query<ShopItem>(sql, new { uid = Session.UserId }).ToList();
    }

    /// <summary>HTML-страница списка покупок для телефона (с отметками «куплено» и сохранением).</summary>
    public static string BuildHtml(IEnumerable<ShopItem> checkedItems)
    {
        var sb = new StringBuilder("[");
        bool any = false;
        foreach (var it in checkedItems)
        {
            var q = $"{it.Qty} {it.Unit}".Trim();
            sb.Append($"{{\"n\":\"{Esc(it.Name)}\",\"q\":\"{Esc(q)}\"}},");
            any = true;
        }
        if (any) sb.Length--;
        sb.Append(']');
        return Html.Replace("ITEMS_PLACEHOLDER", sb.ToString());
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private const string Html = """
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,user-scalable=no">
<title>Список покупок</title>
<style>
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
body{margin:0;background:#eef6ee;font-family:'Segoe UI',Arial,sans-serif}
header{background:#2C5F2D;color:#fff;padding:16px 16px 10px;position:sticky;top:0;z-index:10;box-shadow:0 2px 6px rgba(0,0,0,.25)}
h1{margin:0 0 5px;font-size:21px}
#ctr{font-size:13px;opacity:.82}
.list{padding:10px 12px 90px}
.row{display:flex;align-items:center;background:#fff;border-radius:10px;margin-bottom:9px;padding:14px;box-shadow:0 1px 4px rgba(0,0,0,.08);cursor:pointer;user-select:none;-webkit-user-select:none;transition:background .12s}
.row.done{background:#dff0df}
.cb{width:32px;height:32px;border:2.5px solid #3E8741;border-radius:7px;flex-shrink:0;display:flex;align-items:center;justify-content:center;margin-right:14px;font-size:20px;color:transparent;background:#fff;transition:.12s}
.row.done .cb{background:#3E8741;border-color:#2C5F2D;color:#fff}
.name{flex:1;font-size:18px;line-height:1.3}
.row.done .name{text-decoration:line-through;color:#888}
.qty{font-size:14px;color:#666;white-space:nowrap;margin-left:10px;min-width:54px;text-align:right}
.row.done .qty{color:#aaa;text-decoration:line-through}
.empty{text-align:center;padding:52px 20px;color:#2C5F2D;font-size:19px}
footer{position:fixed;bottom:0;left:0;right:0;background:#fff;border-top:1px solid #ddd;padding:10px 14px;display:flex;gap:10px}
footer button{flex:1;padding:14px;border:none;border-radius:9px;font-size:16px;font-weight:600;cursor:pointer}
#bReset{background:#f0f0f0;color:#555}
#bToggle{background:#3E8741;color:#fff}
</style>
</head>
<body>
<header><h1>🛒 Список покупок</h1><div id="ctr"></div></header>
<div class="list" id="list"></div>
<footer>
  <button id="bReset"  onclick="resetAll()">Сбросить</button>
  <button id="bToggle" onclick="toggleView()">Показать купленные</button>
</footer>
<script>
const items=ITEMS_PLACEHOLDER;
const K='ha_shop_v1';
let done=JSON.parse(localStorage.getItem(K)||'{}');
let showAll=false;
function save(){localStorage.setItem(K,JSON.stringify(done))}
function upd(){
  var b=items.filter(function(_,i){return done[i]}).length;
  document.getElementById('ctr').textContent='Куплено: '+b+' из '+items.length;
}
function esc(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')}
function render(){
  var el=document.getElementById('list');
  var vis=showAll?items:items.filter(function(_,i){return !done[i]});
  if(vis.length===0&&!showAll){
    el.innerHTML='<div class="empty">✅ Всё куплено!<br><small style="color:#666;font-size:15px">Нажмите «Показать купленные»</small></div>';
  }else{
    el.innerHTML=vis.map(function(item){
      var i=items.indexOf(item),d=done[i];
      return '<div class="row'+(d?' done':'')+'" onclick="tog('+i+')">'+
        '<div class="cb">'+(d?'✓':'')+'</div>'+
        '<span class="name">'+esc(item.n)+'</span>'+
        '<span class="qty">'+esc(item.q)+'</span></div>';
    }).join('');
  }
  upd();
}
function tog(i){done[i]=!done[i];save();render()}
function resetAll(){if(confirm('Сбросить все отметки?')){done={};save();render()}}
function toggleView(){
  showAll=!showAll;
  document.getElementById('bToggle').textContent=showAll?'Скрыть купленные':'Показать купленные';
  render();
}
render();
</script>
</body>
</html>
""";
}
