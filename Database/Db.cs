using Microsoft.Data.Sqlite;
using Dapper;
using System.Security.Cryptography;
using System.Text;

namespace HomeAccounting.Database;

public static class Db
{
    public static string DbPath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeAccounting", "homeaccounting.db");

    private static void EnsureDataDir() =>
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        conn.Execute("PRAGMA foreign_keys = ON;");
        return conn;
    }

    /// <summary>SHA-256 хеш пароля в HEX. Пустой пароль → пустая строка.</summary>
    public static string HashPassword(string? password)
    {
        if (string.IsNullOrEmpty(password)) return "";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }

    /// <summary>Консистентный снимок всей базы (все пользователи и таблицы) в файл dest.</summary>
    public static void Backup(string dest)
    {
        EnsureDataDir();
        using (var src = Open())
        using (var dst = new SqliteConnection($"Data Source={dest};Pooling=False"))
        {
            dst.Open();
            src.BackupDatabase(dst);
        }
        // освобождаем файловые дескрипторы из пула, иначе копию не удалить/не переместить
        SqliteConnection.ClearAllPools();
    }

    /// <summary>Проверяет, что файл — это база «Домашнего бюджета» (есть таблица users).</summary>
    public static bool IsValidDb(string path)
    {
        try
        {
            using var c = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            c.Open();
            return c.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('users','accounts')") >= 1;
        }
        catch { return false; }
    }

    /// <summary>Восстанавливает базу из файла src. Текущая база сохраняется рядом как .bak_restore.
    /// Возвращает путь к страховочной копии.</summary>
    public static string RestoreFrom(string src)
    {
        if (!IsValidDb(src))
            throw new InvalidDataException("Файл не является базой данных «Домашнего бюджета».");

        EnsureDataDir();
        // освобождаем все пулы соединений к текущей базе, иначе файл занят
        SqliteConnection.ClearAllPools();

        string safety = DbPath + ".bak_restore";
        if (File.Exists(DbPath)) File.Copy(DbPath, safety, overwrite: true);

        // убрать возможные WAL/SHM текущей базы
        foreach (var ext in new[] { "-wal", "-shm" })
            try { if (File.Exists(DbPath + ext)) File.Delete(DbPath + ext); } catch { }

        File.Copy(src, DbPath, overwrite: true);
        return safety;
    }

    public static void Init()
    {
        EnsureDataDir();
        using var conn = Open();

        CreateBaseTables(conn);     // свежая установка — всё с user_id сразу
        MigrateLegacy(conn);        // обновление с 4.2.6 — ALTER TABLE / перестройка
        CreateIndexes(conn);        // индексы после миграции
        SeedUsers(conn);            // Андрей (по умолчанию) + Таня
        SeedCurrencies(conn);       // грн / $ / € / руб
        AssignOrphansToDefaultUser(conn);
        DeduplicateCategories(conn);
        SeedDefaults(conn);         // справочники для пользователя по умолчанию

        int ver = conn.ExecuteScalar<int>("PRAGMA user_version");
        if (ver < 3) UpgradeAccountIcons(conn);   // одноразово: наглядные иконки вместо одинаковых 💰
        conn.Execute("PRAGMA user_version = 3;");
    }

    // Подбираем выразительную иконку по названию счёта (только там, где осталась дефолтная 💰)
    private static void UpgradeAccountIcons(SqliteConnection conn)
    {
        foreach (var a in conn.Query("SELECT id, name, icon FROM accounts").ToList())
        {
            string icon = (string)(a.icon ?? "");
            if (icon != "💰" && !string.IsNullOrEmpty(icon)) continue;  // выбранную пользователем не трогаем
            string n = ((string)(a.name ?? "")).ToLowerInvariant();
            string nw =
                n.Contains("налич") || n.Contains("готів") || n.Contains("касса") ? "💵" :
                n.Contains("евро")  || n.Contains("євро")  ? "💶" :
                n.Contains("долл")  || n.Contains("usd")   ? "💵" :
                n.Contains("банк") || n.Contains("моно") || n.Contains("приват") || n.Contains("ощад")
                    || n.Contains("укрсиб") || n.Contains("аваль") || n.Contains("карт") ? "💳" :
                "💰";
            if (nw != icon)
                conn.Execute("UPDATE accounts SET icon=@i WHERE id=@id", new { i = nw, id = (long)a.id });
        }
    }

    // ── Схема (свежая установка) ────────────────────────────────────────────
    private static void CreateBaseTables(SqliteConnection conn)
    {
        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS users (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                name          TEXT    NOT NULL UNIQUE,
                password_hash TEXT    NOT NULL DEFAULT '',
                sort_order    INTEGER NOT NULL DEFAULT 0,
                is_default    INTEGER NOT NULL DEFAULT 0,
                remind_q      TEXT    NOT NULL DEFAULT '',
                remind_a      TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS currencies (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                code       TEXT    NOT NULL,
                name       TEXT    NOT NULL DEFAULT '',
                symbol     TEXT    NOT NULL DEFAULT '',
                is_default INTEGER NOT NULL DEFAULT 0,
                sort_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS exchange_rates (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                currency_id INTEGER NOT NULL REFERENCES currencies(id) ON DELETE CASCADE,
                date        TEXT    NOT NULL,
                rate        REAL    NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS accounts (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id         INTEGER NOT NULL DEFAULT 1,
                sort_order      INTEGER NOT NULL DEFAULT 0,
                name            TEXT    NOT NULL,
                note            TEXT    NOT NULL DEFAULT '',
                initial_balance REAL    NOT NULL DEFAULT 0,
                is_hidden       INTEGER NOT NULL DEFAULT 0,
                currency        TEXT    NOT NULL DEFAULT '₴',
                currency_id     INTEGER,
                icon            TEXT    NOT NULL DEFAULT '💰'
            );

            CREATE TABLE IF NOT EXISTS categories (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL DEFAULT 1,
                name    TEXT    NOT NULL,
                type    TEXT    NOT NULL DEFAULT 'expense'
            );

            CREATE TABLE IF NOT EXISTS subcategories (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id     INTEGER NOT NULL DEFAULT 1,
                category_id INTEGER NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS units (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL DEFAULT 1,
                name    TEXT    NOT NULL,
                UNIQUE(user_id, name)
            );

            CREATE TABLE IF NOT EXISTS expenses (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id         INTEGER NOT NULL DEFAULT 1,
                date            TEXT    NOT NULL,
                account_id      INTEGER REFERENCES accounts(id),
                category_id     INTEGER REFERENCES categories(id),
                subcategory_id  INTEGER REFERENCES subcategories(id),
                quantity        REAL    NOT NULL DEFAULT 1,
                unit_id         INTEGER REFERENCES units(id),
                amount          REAL    NOT NULL DEFAULT 0,
                discount        REAL    NOT NULL DEFAULT 0,
                currency_id     INTEGER,
                rate            REAL    NOT NULL DEFAULT 1,
                note            TEXT    NOT NULL DEFAULT '',
                receipt_item_name TEXT  NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS incomes (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id         INTEGER NOT NULL DEFAULT 1,
                date            TEXT    NOT NULL,
                account_id      INTEGER REFERENCES accounts(id),
                category_id     INTEGER REFERENCES categories(id),
                subcategory_id  INTEGER REFERENCES subcategories(id),
                quantity        REAL    NOT NULL DEFAULT 1,
                unit_id         INTEGER REFERENCES units(id),
                amount          REAL    NOT NULL DEFAULT 0,
                currency_id     INTEGER,
                rate            REAL    NOT NULL DEFAULT 1,
                note            TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS transfers (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id          INTEGER NOT NULL DEFAULT 1,
                date             TEXT    NOT NULL,
                from_account_id  INTEGER REFERENCES accounts(id),
                to_account_id    INTEGER REFERENCES accounts(id),
                amount           REAL    NOT NULL DEFAULT 0,
                currency_id      INTEGER,
                note             TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS item_mappings (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id         INTEGER NOT NULL DEFAULT 1,
                name_key        TEXT    NOT NULL,
                category_id     INTEGER REFERENCES categories(id),
                subcategory_id  INTEGER REFERENCES subcategories(id),
                unit_id         INTEGER REFERENCES units(id),
                UNIQUE(user_id, name_key)
            );

            CREATE TABLE IF NOT EXISTS budgets (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id        INTEGER NOT NULL DEFAULT 1,
                type           TEXT    NOT NULL DEFAULT 'expense',
                year           INTEGER NOT NULL,
                month          INTEGER NOT NULL,
                category_id    INTEGER REFERENCES categories(id),
                subcategory_id INTEGER REFERENCES subcategories(id),
                currency_id    INTEGER,
                plan           REAL    NOT NULL DEFAULT 0,
                note           TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS debts (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id       INTEGER NOT NULL DEFAULT 1,
                kind          TEXT    NOT NULL DEFAULT 'debtor',   -- debtor | creditor
                account_id    INTEGER REFERENCES accounts(id),
                date          TEXT    NOT NULL,
                counterparty  TEXT    NOT NULL DEFAULT '',
                currency_id   INTEGER,
                amount        REAL    NOT NULL DEFAULT 0,
                amount_back   REAL    NOT NULL DEFAULT 0,
                percent       REAL    NOT NULL DEFAULT 0,
                is_closed     INTEGER NOT NULL DEFAULT 0,
                date_close    TEXT    NOT NULL DEFAULT '',
                note          TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS shopping_marks (
                user_id  INTEGER NOT NULL,
                name     TEXT    NOT NULL,
                category TEXT    NOT NULL DEFAULT '',
                qty      TEXT    NOT NULL DEFAULT '1',
                PRIMARY KEY(user_id, name, category)
            );

            CREATE TABLE IF NOT EXISTS deposits (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id       INTEGER NOT NULL DEFAULT 1,
                name          TEXT    NOT NULL DEFAULT '',
                account_id    INTEGER REFERENCES accounts(id),
                currency_id   INTEGER,
                amount        REAL    NOT NULL DEFAULT 0,
                rate          REAL    NOT NULL DEFAULT 0,
                open_date     TEXT    NOT NULL DEFAULT '',
                close_date    TEXT    NOT NULL DEFAULT '',
                note          TEXT    NOT NULL DEFAULT ''
            );
        ");
    }

    // Индексы создаём ПОСЛЕ миграции — у старой схемы колонки user_id ещё нет
    private static void CreateIndexes(SqliteConnection conn)
    {
        conn.Execute(@"
            CREATE INDEX IF NOT EXISTS ix_expenses_user     ON expenses(user_id);
            CREATE INDEX IF NOT EXISTS ix_expenses_date     ON expenses(date);
            CREATE INDEX IF NOT EXISTS ix_expenses_account  ON expenses(account_id);
            CREATE INDEX IF NOT EXISTS ix_expenses_category ON expenses(category_id);
            CREATE INDEX IF NOT EXISTS ix_incomes_user      ON incomes(user_id);
            CREATE INDEX IF NOT EXISTS ix_incomes_date      ON incomes(date);
            CREATE INDEX IF NOT EXISTS ix_incomes_account   ON incomes(account_id);
            CREATE INDEX IF NOT EXISTS ix_transfers_user    ON transfers(user_id);
            CREATE INDEX IF NOT EXISTS ix_accounts_user     ON accounts(user_id);
            CREATE INDEX IF NOT EXISTS ix_categories_user   ON categories(user_id);
            CREATE INDEX IF NOT EXISTS ix_budgets_user      ON budgets(user_id);
            CREATE INDEX IF NOT EXISTS ix_debts_user        ON debts(user_id);
        ");
    }

    // ── Миграция со старой схемы 4.2.6 ──────────────────────────────────────
    private static bool ColumnExists(SqliteConnection conn, string table, string column)
    {
        var cols = conn.Query<string>($"SELECT name FROM pragma_table_info('{table}')");
        return cols.Any(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddColumnIfMissing(SqliteConnection conn, string table, string column, string ddl)
    {
        if (!ColumnExists(conn, table, column))
            try { conn.Execute($"ALTER TABLE {table} ADD COLUMN {ddl}"); } catch { }
    }

    private static void MigrateLegacy(SqliteConnection conn)
    {
        // receipt_item_name / currency / icon (из 4.2.6)
        AddColumnIfMissing(conn, "expenses", "receipt_item_name", "receipt_item_name TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(conn, "accounts", "currency",          "currency TEXT NOT NULL DEFAULT '₴'");
        AddColumnIfMissing(conn, "accounts", "icon",              "icon TEXT NOT NULL DEFAULT '💰'");

        // user_id во все личные таблицы
        foreach (var t in new[] { "accounts", "categories", "subcategories",
                                  "expenses", "incomes", "transfers" })
            AddColumnIfMissing(conn, t, "user_id", "user_id INTEGER NOT NULL DEFAULT 1");

        // currency_id / rate в операции и счета
        AddColumnIfMissing(conn, "accounts",  "currency_id", "currency_id INTEGER");
        AddColumnIfMissing(conn, "expenses",  "currency_id", "currency_id INTEGER");
        AddColumnIfMissing(conn, "expenses",  "rate",        "rate REAL NOT NULL DEFAULT 1");
        AddColumnIfMissing(conn, "incomes",   "currency_id", "currency_id INTEGER");
        AddColumnIfMissing(conn, "incomes",   "rate",        "rate REAL NOT NULL DEFAULT 1");
        AddColumnIfMissing(conn, "transfers", "currency_id", "currency_id INTEGER");

        // units: старая схема UNIQUE(name) → новая UNIQUE(user_id,name)
        if (TableExists(conn, "units") && !ColumnExists(conn, "units", "user_id"))
            RebuildUnits(conn);

        // item_mappings: старая схема UNIQUE(name_key) → новая UNIQUE(user_id,name_key)
        if (TableExists(conn, "item_mappings") && !ColumnExists(conn, "item_mappings", "user_id"))
            RebuildItemMappings(conn);
    }

    private static bool TableExists(SqliteConnection conn, string table) =>
        conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@t", new { t = table }) > 0;

    private static void RebuildUnits(SqliteConnection conn)
    {
        conn.Execute("PRAGMA foreign_keys = OFF;");
        using var tr = conn.BeginTransaction();
        conn.Execute(@"
            CREATE TABLE units_new (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL DEFAULT 1,
                name    TEXT    NOT NULL,
                UNIQUE(user_id, name));
            INSERT INTO units_new(id, user_id, name) SELECT id, 1, name FROM units;
            DROP TABLE units;
            ALTER TABLE units_new RENAME TO units;", transaction: tr);
        tr.Commit();
        conn.Execute("PRAGMA foreign_keys = ON;");
    }

    private static void RebuildItemMappings(SqliteConnection conn)
    {
        conn.Execute("PRAGMA foreign_keys = OFF;");
        using var tr = conn.BeginTransaction();
        conn.Execute(@"
            CREATE TABLE item_mappings_new (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id        INTEGER NOT NULL DEFAULT 1,
                name_key       TEXT    NOT NULL,
                category_id    INTEGER,
                subcategory_id INTEGER,
                unit_id        INTEGER,
                UNIQUE(user_id, name_key));
            INSERT INTO item_mappings_new(id, user_id, name_key, category_id, subcategory_id, unit_id)
                SELECT id, 1, name_key, category_id, subcategory_id, unit_id FROM item_mappings;
            DROP TABLE item_mappings;
            ALTER TABLE item_mappings_new RENAME TO item_mappings;", transaction: tr);
        tr.Commit();
        conn.Execute("PRAGMA foreign_keys = ON;");
    }

    // ── Сидинг пользователей ────────────────────────────────────────────────
    private static void SeedUsers(SqliteConnection conn)
    {
        var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM users");
        if (count == 0)
        {
            // Андрей — пользователь по умолчанию (наследует данные из 4.2.6)
            conn.Execute(
                "INSERT INTO users(name,password_hash,sort_order,is_default,remind_q,remind_a) VALUES(@n,@p,1,1,@q,@a)",
                new { n = "Андрей", p = HashPassword("njkmrjz"), q = "Mother name?", a = "Tanya" });
            conn.Execute(
                "INSERT INTO users(name,password_hash,sort_order,is_default,remind_q,remind_a) VALUES(@n,@p,2,0,@q,@a)",
                new { n = "Таня", p = HashPassword("6228"), q = "Любимый цвет", a = "Фиолетовый" });
        }
        else
        {
            // гарантируем наличие пользователя по умолчанию
            var hasDefault = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM users WHERE is_default=1") > 0;
            if (!hasDefault)
                conn.Execute("UPDATE users SET is_default=1 WHERE id=(SELECT MIN(id) FROM users)");
        }
    }

    public static int DefaultUserId(SqliteConnection conn) =>
        conn.ExecuteScalar<int>("SELECT id FROM users WHERE is_default=1 ORDER BY id LIMIT 1");

    // ── Сидинг валют ────────────────────────────────────────────────────────
    private static void SeedCurrencies(SqliteConnection conn)
    {
        if (conn.ExecuteScalar<long>("SELECT COUNT(*) FROM currencies") > 0) return;
        var rows = new (string code, string name, string sym, int def)[]
        {
            ("UAH", "Гривны",  "₴", 1),
            ("USD", "Доллары", "$", 0),
            ("EUR", "Евро",    "€", 0),
            ("RUB", "Рубли",   "р", 0),
        };
        int order = 1;
        foreach (var r in rows)
            conn.Execute(
                "INSERT INTO currencies(code,name,symbol,is_default,sort_order) VALUES(@c,@n,@s,@d,@o)",
                new { c = r.code, n = r.name, s = r.sym, d = r.def, o = order++ });
    }

    private static void AssignOrphansToDefaultUser(SqliteConnection conn)
    {
        int uid = DefaultUserId(conn);
        foreach (var t in new[] { "accounts", "categories", "subcategories",
                                  "expenses", "incomes", "transfers", "item_mappings" })
            try { conn.Execute($"UPDATE {t} SET user_id=@uid WHERE user_id IS NULL OR user_id=0", new { uid }); }
            catch { }

        // currency_id по умолчанию = валюта по умолчанию
        int defCur = conn.ExecuteScalar<int>("SELECT id FROM currencies WHERE is_default=1 ORDER BY id LIMIT 1");
        foreach (var t in new[] { "accounts", "expenses", "incomes", "transfers" })
            try { conn.Execute($"UPDATE {t} SET currency_id=@c WHERE currency_id IS NULL", new { c = defCur }); }
            catch { }
    }

    private static void DeduplicateCategories(SqliteConnection conn)
    {
        conn.Execute("PRAGMA foreign_keys = OFF");
        // дубликаты ищем в пределах одного пользователя (name,type,user_id)
        conn.Execute(@"
            UPDATE expenses SET category_id = (
                SELECT MIN(c2.id) FROM categories c2
                WHERE c2.name = (SELECT c1.name FROM categories c1 WHERE c1.id = expenses.category_id)
                  AND c2.type = (SELECT c1.type FROM categories c1 WHERE c1.id = expenses.category_id)
                  AND c2.user_id = (SELECT c1.user_id FROM categories c1 WHERE c1.id = expenses.category_id)
            ) WHERE category_id IS NOT NULL;

            UPDATE incomes SET category_id = (
                SELECT MIN(c2.id) FROM categories c2
                WHERE c2.name = (SELECT c1.name FROM categories c1 WHERE c1.id = incomes.category_id)
                  AND c2.type = (SELECT c1.type FROM categories c1 WHERE c1.id = incomes.category_id)
                  AND c2.user_id = (SELECT c1.user_id FROM categories c1 WHERE c1.id = incomes.category_id)
            ) WHERE category_id IS NOT NULL;

            DELETE FROM categories WHERE id NOT IN (
                SELECT MIN(id) FROM categories GROUP BY user_id, name, type
            );
        ");
        conn.Execute("PRAGMA foreign_keys = ON");
    }

    // ── Справочники по умолчанию (для пользователя по умолчанию) ─────────────
    private static void SeedDefaults(SqliteConnection conn)
    {
        int uid = DefaultUserId(conn);

        var units = new[] { "шт.", "кг.", "г.", "л.", "мл.", "уп.", "пак.", "бут.",
                            "батон", "коробка", "комплект", "таблеток", "пластины" };
        foreach (var u in units)
            conn.Execute("INSERT OR IGNORE INTO units(user_id,name) VALUES(@uid,@n)", new { uid, n = u });

        // ── Расходы ───────────────────────────────────────────────────────────
        SeedCat(conn, uid, "Коммунальные услуги", "expense", new[] {
            "+38 096 234 73 61","Отопление","Вода холодная","Квартплата","Мусор","Электроэнергия",
            "Газ","ГазТруба","ТеплоАбон","Укртелеком","+38 096 260 34 09","Домофон",
            "+38 068 235 49 07","Подъезд","Комиссия","ВодаАбон","WWW","Страховка"
        });
        SeedCat(conn, uid, "Продукты питания", "expense", new[] {
            "Алкоголь","Бананы","Гречка","Колбаса","Сыр","Филе","Хлеб","Яйца","Десерт",
            "Филе курицы","Шейка","Масло сливочное","Пельмени","Булочка","Морковь","Молоко",
            "Пирог","Творог","Мед","Пряники","Какао","Конфеты","Свекла","Фарш","Лимон",
            "Напитки","Печенье","Батон","Чай","Балык куриный","Бисквит","Апельсины","Кофе",
            "Окорок","Лук","Рыба","Вода минеральная","Сосиски","Буженина","Огурцы","Кафе",
            "Йогурт","Селёдка","Капуста","Грейпфрут","Кефир","Приправы и специи","Рис",
            "Изюм","Сахар ванильный","Консервы","KFC","Сыр плавленный","Шоколад",
            "Ананас кубики","Дрожжи","Сметана","Масло растительное","Грудинка","Торт",
            "Яблоки","Лаваш","Перец","Кекс","Балык","Соль","Чеснок","Жвачка","Вафли",
            "Грибы","Багет","Икра","Мясо","Вареники","Тесто","Пицца","Копчёности",
            "Мандарины","Сгущёнка","Сгущёнка с какао","Уксус","Семечки","Майонез","Крупа",
            "маслины","Киви","Хрен","Креветки","Авокадо","Соус","Картофель","Пирожное",
            "Сало","Сахар","Полуфабрикаты","Гранат","Забытое","Корейка","Овощи","Мороженое",
            "Виноград","Хурма","Бекон","Горчица","Шашлык","Зелень","Кетчуп","Масло оливковое",
            "Персики","Нектарин","Помидоры","Томатная паста","Рёбра","Мука","Чиа семена",
            "Заправка","Мак","Бакалея","Мидии","Миндаль","Курага","Ошеек свиной","Орехи",
            "Печень куриная","Печень","Ряженка","Пирожки","Сливки","Зефир","Курица","Нагетсы",
            "Кислота лимонная","Крекер","Котлеты","Арбуз","Шинка","Сок","Перекус","Сардельки",
            "Картофель чищеный","Финики","Набор овощей очищенных","Макароны","Груша","Маринад",
            "Фрукты","Блины","Локшина","Сухари","Оливки","Желатин","Клубника","Пасха","Кэшью",
            "Ананас","Язык","Каша","Черешня","Заготовка для пиццы","Салаты","Нутелла","Крахмал",
            "Гренки","Имбирь","Баранки","Масло шоколадное","Сухофрукты","Памело"
        });
        SeedCat(conn, uid, "Семья", "expense", new[] {
            "Таня","Даня","Тамара","Наоми","Таня Павловна","Наташа"
        });
        SeedCat(conn, uid, "Хозяйственные товары", "expense", new[] {
            "Кувшин","Бальзам","Леска для бензокосы","Полиэтиленовый пакет","Семена",
            "Средство для мытья посуды","Гель для душа","Зубная паста","Пакет бумажный подарочный",
            "Мыло","Посуда","Туалетная бумага","Стиральный порошок","Зубная щётка","Вешалка",
            "Стельки","Таз","Пергамент","Шампунь","Гвозди","Диски ватные","Ополаскиватель",
            "Губка","Салфетки","Свечи на торт","Полотенца","Резинка","Лампочка","Сверло",
            "Электротовары","дюбель","Рукав для запекания","Фольга","Зажигалка",
            "Крышки политиленовые на банки","Скотч","Прокладки ежедневные","Дезодорант",
            "Пакеты для мусора","Маска","Банки","Крышки для консервирования","Инструмент",
            "Антиперспирант","Бытовая химия","Спички","Зубочистки","Земля для рассады",
            "Пена для ванны","Отбеливатель","Сет под горячее","Термометр кулинарный","Мяч",
            "Чистящие средства","Ключ","Замок","Фонарь","Батарейки","Рукавицы рабочие","Сумка",
            "Кассета для рассады","Орехокол","Пластилин","Гель","Рюмка","Сантехника","Душ",
            "Кондиционер","Гирлянда","Лейка","Шампура","Форма для льда","Щетка","Ведро",
            "Раптор","Лопата","Клей","Труба","Доска разделочная","Ухват для банок",
            "Чехол для стирки","Перчатки","Вилки","Нож","трубки для коктейлей","Силикон",
            "Горшок цветочный","Контейнер","Сверло по керамике","полка","Краска",
            "Крышка для унитаза","Кисть","Вантуз","Держак туалетной бумаги","Сушка для белья",
            "Кастрюля","Сульсена","Средство для мытья полов","Одеколон","Кофеварка","Сковорода",
            "Палочки для шашлыка","Ящик","Зонт"
        });
        SeedCat(conn, uid, "Аптека", "expense", new[] {
            "Азитромицин","Тутоназа","Тамсин форте","Прегабалин","Флюколд-Н","Анальгин","Вата",
            "Аторис","Вогер","Глутаргин","Нейрорубин-форте","Хлорофилипт","Прополис",
            "Хлорид натрия","Эвкалипт настойка","Моксогама","Фервекс","Ренохелс","Нимесил",
            "Таблета","Каптоприл","Клопидогрель","Серрата","Солемакс","Аугментин",
            "Энтерожермина форте","Комбигрипп","Корвалмент","Перчатки","Витамин-D","Роксера",
            "Жень-шень","Цинк","Аспаркам","Нить зубная","Ополаскиватель зубной","Салфетки",
            "Л-тирозин","Детский крем","Магний","Росторопша","Алохол","Эссенциале форте",
            "Бромгексин","Мукалтин","Азитро Сандос","Медь","Фурацилин","Респикс спрей",
            "Ролиноз","Умколор","Септолете","Силденафил","Рибоксин","Дротаверин","Толокнянка",
            "Сорбент+","Флуконазол","Пластырь","Прокладки","Спирт","Пимафуцин","Тайгерон",
            "Фурадонин","Цистон","Ингалар","Ацикловир","Фосфор","Корамаг","Магникор",
            "Предуктал","Этацизин","Адвокард","Бринтелликс","Сульсена","Чорница форте","Хром",
            "Эндокрин","Крем-гель Скипидар для тела","Эфирное масло","Валавир","Магне-В6",
            "Черника добавка","Л-лизин","Бурш.кислота","Пробионекс Йогурт","Магнефар",
            "Астрацитрон","Нокспрей","Л-карнитин","Олидетрим","Солгар","Список",
            "Нитроглицерин","Вальтрекс","Лейкопластырь","Ромашка","Витаксон","Грелка",
            "Герпевир","Марля","Диклофенак","Глицесид","Золопент","Кливас","Лактиале",
            "Финлепсин","Дексалгин","Стрептоцид","Оверин","Тутукон","Ренгалин","Сумамед",
            "Ибупрофен","Магникум","Пирацетам","Масло чайного дерева","Касторовое масло",
            "Пантенол","Спасатель","Кардиомагнил","Активированный уголь","Вишневского",
            "Дуодарт","Облепиховое масло","Салициловая кислота","Антитусин"
        });
        SeedCat(conn, uid, "Транспорт", "expense", new[] {
            "Поезд","Маршрутка","Метро","Такси","Электричка"
        });
        SeedCat(conn, uid, "Медицина", "expense", new[] {
            "Прием врача","Стоматолог","Вагинальные шарики","УЗИ","ЭКГ","Анализы","КТ",
            "Маска для волос","Марля","Очки","Ремонт очков","Футляр для очков","Вибратор",
            "Контейнер для анализов","Вызов врача","Книга","Ингалятор","Кварцевая лампа",
            "Бахилы","Лекарство","Помпа","Аппарат для измерения давления"
        });
        SeedCat(conn, uid, "Техника", "expense", new[] {
            "Адаптер питания","Фитнес-браслет","Удлинитель","Карта памяти","Видеокамера",
            "Вилка","Розетка","Выключатель","Кабель","Роутер","Батарейки","Светильник",
            "Фонарик","Крокодилы","Индикаторная отвёртка","Диск на болгарку","Миксер",
            "Холодильник","Игра","Плэйстэйшн-5","Шурупы","Газовая горелка","Зарядное устройство",
            "Дюбель","Блендер","Комплектующие","Ключ разводной","Пасатижи","Полотно для ножевки",
            "Фильтр для очистки воды","Фум лента","Круглогубцы","Нож","Прочее","Чайник","Фен","Кондиционер"
        });
        SeedCat(conn, uid, "потери", "expense", new[] {
            "Разница курсов","Подгонка","Уменьшение лимита","Банк приворовывает",
            "Воровство в Велмарте","Судовой сбор","может потерял"
        });
        SeedCat(conn, uid, "Игнатий", "expense", new[] {
            "Куриные лапы","Свиные кости","Шейки куриные","Корм сухой","Корм жидкий","Травка",
            "Печень","ветеринар","перевозка","Гигиенический наполнитель","Руковица для вычёсывания",
            "Игрушка","Филе курицы","Творог","Светильник","Когтеточка","Лоток","Шарик",
            "Полотенце","Совок","Капли против блох","Ошейник","Переноска","Миска","Шампунь"
        });
        SeedCat(conn, uid, "Банки", "expense", new[] {
            "Монобанк","Ощад2","Приват","Ощадбанк","Аваль","Укрсиббанк","Сбербанк"
        });
        SeedCat(conn, uid, "Компьютер", "expense", new[] {
            "ИИ","Аккумулятор","Интернет мастер","Веб-камера","Коврик для мышки",
            "ABBYY Screenshot Reader","Обжим кабеля","HomeBuh"
        });
        SeedCat(conn, uid, "Услуги", "expense", new[] {
            "Туалет","Благотворительность","Почта","Ремонт техники"
        });
        SeedCat(conn, uid, "Обувь", "expense", new[] {
            "Кроссовки","Ботинки"
        });
        SeedCat(conn, uid, "Комиссия", "expense", new[] {
            "Подгонка","Обмен валюты","Перенос средств между счетами","Сдача билетов"
        });
        SeedCat(conn, uid, "Развлечения", "expense", new[] {
            "Игра","Игрушка","Ёлка","Самокаты","Семена","Экскурсия"
        });
        SeedCat(conn, uid, "Одежда", "expense", new[] {
            "Носки","Ремень","Кепка","Брюки","Рубашка"
        });
        SeedCat(conn, uid, "Мебель", "expense", new[] {
            "Полка","Фурнитура","Постельное","Шведская стенка","Подушка"
        });
        SeedCat(conn, uid, "Канцелярия", "expense", new[] {
            "Бумага","Увеличительное стекло","скобы к степлеру","Скотч","Степлер"
        });
        // legacy categories kept for backward compat
        SeedCat(conn, uid, "Одежда и обувь", "expense", Array.Empty<string>());
        SeedCat(conn, uid, "Связь",           "expense", Array.Empty<string>());
        SeedCat(conn, uid, "Здоровье",        "expense", Array.Empty<string>());
        SeedCat(conn, uid, "Прочее",          "expense", Array.Empty<string>());

        // ── Доходы ───────────────────────────────────────────────────────────
        SeedCat(conn, uid, "Зарплата",            "income", Array.Empty<string>());
        SeedCat(conn, uid, "Пенсия",             "income", Array.Empty<string>());
        SeedCat(conn, uid, "Социальные выплаты", "income", Array.Empty<string>());
        SeedCat(conn, uid, "Подарки",            "income", Array.Empty<string>());
        SeedCat(conn, uid, "Прочие доходы",      "income", Array.Empty<string>());
    }

    private static void SeedCat(SqliteConnection conn, int uid, string cat, string type, string[] subs)
    {
        conn.Execute(
            "INSERT INTO categories(user_id,name,type) SELECT @uid,@n,@t WHERE NOT EXISTS (SELECT 1 FROM categories WHERE user_id=@uid AND name=@n AND type=@t)",
            new { uid, n = cat, t = type });
        foreach (var s in subs)
            conn.Execute(
                @"INSERT INTO subcategories(user_id,category_id,name)
                  SELECT @uid,c.id,@s FROM categories c WHERE c.user_id=@uid AND c.name=@n AND c.type=@t
                  AND NOT EXISTS (SELECT 1 FROM subcategories x WHERE x.category_id=c.id AND x.name=@s)",
                new { uid, n = cat, t = type, s });
    }
}
