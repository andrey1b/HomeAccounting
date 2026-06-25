using System.Windows;
using System.Windows.Controls;

namespace HomeAccounting.Views;

public partial class CategoryManagerWindow : Window
{
    private readonly string _type;

    public CategoryManagerWindow(string type)
    {
        InitializeComponent();
        _type = type;
        ApplyLoc();
        LoadCategories();
    }

    private void ApplyLoc()
    {
        Title = AppLoc.T(_type == "expense" ? "dlg_cats_expense_title" : "dlg_cats_income_title");
        TbLblCategories.Text    = AppLoc.T("lbl_categories");
        TbLblSubcategories.Text = AppLoc.T("lbl_subcategories");
        BtnAddCatBtn.Content    = AppLoc.T("btn_add");
        BtnRenameCatBtn.Content = AppLoc.T("btn_edit");
        BtnDeleteCatBtn.Content = AppLoc.T("btn_delete");
        BtnAddSubBtn.Content    = AppLoc.T("btn_add");
        BtnRenameSubBtn.Content = AppLoc.T("btn_edit");
        BtnDeleteSubBtn.Content = AppLoc.T("btn_delete");
    }

    private void LoadCategories()
    {
        LbCategories.ItemsSource       = CategoryService.GetAll(_type);
        LbCategories.DisplayMemberPath = "Name";
        LbSubcategories.ItemsSource    = null;
    }

    private void LbCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LbCategories.SelectedItem is not Category cat) { LbSubcategories.ItemsSource = null; return; }
        LbSubcategories.ItemsSource       = CategoryService.GetSubcategories(cat.Id);
        LbSubcategories.DisplayMemberPath = "Name";
    }

    // ── Categories ────────────────────────────────────────────────────────────

    private void BtnAddCat_Click(object sender, RoutedEventArgs e)
    {
        var name = ShowInput(AppLoc.T("btn_add"), "");
        if (string.IsNullOrEmpty(name)) return;
        CategoryService.AddCategory(name, _type);
        LoadCategories();
    }

    private void BtnRenameCat_Click(object sender, RoutedEventArgs e)
    {
        if (LbCategories.SelectedItem is not Category cat) return;
        var name = ShowInput(AppLoc.T("btn_edit"), cat.Name);
        if (string.IsNullOrEmpty(name) || name == cat.Name) return;
        CategoryService.UpdateCategory(cat.Id, name);
        LoadCategories();
    }

    private void BtnDeleteCat_Click(object sender, RoutedEventArgs e)
    {
        if (LbCategories.SelectedItem is not Category cat) return;
        var msg = AppLoc.T("msg_confirm_del_cat", "name", cat.Name);
        if (MessageBox.Show(msg, AppLoc.T("msg_confirm"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        CategoryService.DeleteCategory(cat.Id);
        LoadCategories();
    }

    // ── Subcategories ─────────────────────────────────────────────────────────

    private void BtnAddSub_Click(object sender, RoutedEventArgs e)
    {
        if (LbCategories.SelectedItem is not Category cat)
        { MessageBox.Show(AppLoc.T("msg_select_cat_first")); return; }
        var name = ShowInput(AppLoc.T("btn_add"), "");
        if (string.IsNullOrEmpty(name)) return;
        CategoryService.AddSubcategory(cat.Id, name);
        LbCategories_SelectionChanged(null!, null!);
    }

    private void BtnRenameSub_Click(object sender, RoutedEventArgs e)
    {
        if (LbSubcategories.SelectedItem is not Subcategory sub) return;
        var name = ShowInput(AppLoc.T("btn_edit"), sub.Name);
        if (string.IsNullOrEmpty(name) || name == sub.Name) return;
        CategoryService.UpdateSubcategory(sub.Id, name);
        LbCategories_SelectionChanged(null!, null!);
    }

    private void BtnDeleteSub_Click(object sender, RoutedEventArgs e)
    {
        if (LbSubcategories.SelectedItem is not Subcategory sub) return;
        var msg = AppLoc.T("msg_confirm_del_subcat", "name", sub.Name);
        if (MessageBox.Show(msg, AppLoc.T("msg_confirm"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        CategoryService.DeleteSubcategory(sub.Id);
        LbCategories_SelectionChanged(null!, null!);
    }

    // ── Input dialog ──────────────────────────────────────────────────────────

    private string? ShowInput(string title, string defaultValue)
    {
        var win = new Window
        {
            Title  = title, Width = 360, Height = 110,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner  = this, ResizeMode = ResizeMode.NoResize
        };
        var sp  = new StackPanel { Margin = new Thickness(12) };
        var tb  = new TextBox { Text = defaultValue };
        var row = new StackPanel { Orientation = Orientation.Horizontal,
                                   HorizontalAlignment = HorizontalAlignment.Right,
                                   Margin = new Thickness(0, 8, 0, 0) };
        var ok     = new Button { Content = "OK", Width = 80, IsDefault = true };
        var cancel = new Button { Content = AppLoc.T("btn_cancel"), Width = 80,
                                  Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        row.Children.Add(ok);
        row.Children.Add(cancel);
        sp.Children.Add(tb);
        sp.Children.Add(row);
        win.Content = sp;
        win.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        string? result = null;
        ok.Click     += (_, _) => { result = tb.Text.Trim(); win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.ShowDialog();
        return result;
    }
}
