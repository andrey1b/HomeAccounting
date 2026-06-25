using Microsoft.Data.Sqlite;
using Dapper;

namespace HomeAccounting.Database;

public static class Db
{
    public static string DbPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "homeaccounting.db");

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        conn.Execute("PRAGMA foreign_keys = ON;");
        return conn;
    }

    public static void Init()
    {
        using var conn = Open();
        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS accounts (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                sort_order      INTEGER NOT NULL DEFAULT 0,
                name            TEXT    NOT NULL,
                note            TEXT    NOT NULL DEFAULT '',
                initial_balance REAL    NOT NULL DEFAULT 0,
                is_hidden       INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS categories (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT    NOT NULL,
                type TEXT    NOT NULL DEFAULT 'expense'
            );

            CREATE TABLE IF NOT EXISTS subcategories (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                category_id INTEGER NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
                name        TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS units (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS expenses (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                date            TEXT    NOT NULL,
                account_id      INTEGER REFERENCES accounts(id),
                category_id     INTEGER REFERENCES categories(id),
                subcategory_id  INTEGER REFERENCES subcategories(id),
                quantity        REAL    NOT NULL DEFAULT 1,
                unit_id         INTEGER REFERENCES units(id),
                amount          REAL    NOT NULL DEFAULT 0,
                discount        REAL    NOT NULL DEFAULT 0,
                note            TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS incomes (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                date            TEXT    NOT NULL,
                account_id      INTEGER REFERENCES accounts(id),
                category_id     INTEGER REFERENCES categories(id),
                subcategory_id  INTEGER REFERENCES subcategories(id),
                quantity        REAL    NOT NULL DEFAULT 1,
                unit_id         INTEGER REFERENCES units(id),
                amount          REAL    NOT NULL DEFAULT 0,
                note            TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS transfers (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                date             TEXT    NOT NULL,
                from_account_id  INTEGER REFERENCES accounts(id),
                to_account_id    INTEGER REFERENCES accounts(id),
                amount           REAL    NOT NULL DEFAULT 0,
                note             TEXT    NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS item_mappings (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                name_key        TEXT    NOT NULL UNIQUE,
                category_id     INTEGER REFERENCES categories(id),
                subcategory_id  INTEGER REFERENCES subcategories(id),
                unit_id         INTEGER REFERENCES units(id)
            );

            CREATE INDEX IF NOT EXISTS ix_expenses_date        ON expenses(date);
            CREATE INDEX IF NOT EXISTS ix_expenses_account     ON expenses(account_id);
            CREATE INDEX IF NOT EXISTS ix_expenses_category    ON expenses(category_id);
            CREATE INDEX IF NOT EXISTS ix_incomes_date         ON incomes(date);
            CREATE INDEX IF NOT EXISTS ix_incomes_account      ON incomes(account_id);
        ");

        // Migration: add receipt_item_name column if not yet present
        try { conn.Execute("ALTER TABLE expenses ADD COLUMN receipt_item_name TEXT NOT NULL DEFAULT ''"); }
        catch { /* column already exists */ }

        // Migration: add currency and icon columns to accounts
        try { conn.Execute("ALTER TABLE accounts ADD COLUMN currency TEXT NOT NULL DEFAULT '₴'"); }
        catch { /* column already exists */ }
        try { conn.Execute("ALTER TABLE accounts ADD COLUMN icon TEXT NOT NULL DEFAULT '💰'"); }
        catch { /* column already exists */ }

        DeduplicateCategories(conn);
        SeedDefaults(conn);
    }

    private static void DeduplicateCategories(SqliteConnection conn)
    {
        // Temporarily disable FK checks so we can clean up duplicates safely
        conn.Execute("PRAGMA foreign_keys = OFF");

        // Redirect all expense/income references to the surviving (min-id) category
        conn.Execute(@"
            UPDATE expenses SET category_id = (
                SELECT MIN(c2.id) FROM categories c2
                WHERE c2.name = (SELECT c1.name FROM categories c1 WHERE c1.id = expenses.category_id)
                  AND c2.type = (SELECT c1.type FROM categories c1 WHERE c1.id = expenses.category_id)
            ) WHERE category_id IS NOT NULL;

            UPDATE incomes SET category_id = (
                SELECT MIN(c2.id) FROM categories c2
                WHERE c2.name = (SELECT c1.name FROM categories c1 WHERE c1.id = incomes.category_id)
                  AND c2.type = (SELECT c1.type FROM categories c1 WHERE c1.id = incomes.category_id)
            ) WHERE category_id IS NOT NULL;

            DELETE FROM categories WHERE id NOT IN (
                SELECT MIN(id) FROM categories GROUP BY name, type
            );
        ");

        conn.Execute("PRAGMA foreign_keys = ON");
    }

    private static void SeedDefaults(SqliteConnection conn)
    {
        var units = new[] { "шт.", "кг.", "г.", "л.", "мл.", "уп.", "пак.", "бут.",
                            "батон", "коробка", "комплект", "таблеток", "пластины" };
        foreach (var u in units)
            conn.Execute("INSERT OR IGNORE INTO units(name) VALUES(@n)", new { n = u });

        // ── Расходы ───────────────────────────────────────────────────────────
        SeedCat(conn, "Коммунальные услуги", "expense", new[] {
            "+38 096 234 73 61","Отопление","Вода холодная","Квартплата","Мусор","Электроэнергия",
            "Газ","ГазТруба","ТеплоАбон","Укртелеком","+38 096 260 34 09","Домофон",
            "+38 068 235 49 07","Подъезд","Комиссия","ВодаАбон","WWW","Страховка"
        });
        SeedCat(conn, "Продукты питания", "expense", new[] {
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
        SeedCat(conn, "Семья", "expense", new[] {
            "Таня","Даня","Тамара","Наоми","Таня Павловна","Наташа"
        });
        SeedCat(conn, "Хозяйственные товары", "expense", new[] {
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
        SeedCat(conn, "Аптека", "expense", new[] {
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
        SeedCat(conn, "Транспорт", "expense", new[] {
            "Поезд","Маршрутка","Метро","Такси","Электричка"
        });
        SeedCat(conn, "Медицина", "expense", new[] {
            "Прием врача","Стоматолог","Вагинальные шарики","УЗИ","ЭКГ","Анализы","КТ",
            "Маска для волос","Марля","Очки","Ремонт очков","Футляр для очков","Вибратор",
            "Контейнер для анализов","Вызов врача","Книга","Ингалятор","Кварцевая лампа",
            "Бахилы","Лекарство","Помпа","Аппарат для измерения давления"
        });
        SeedCat(conn, "Техника", "expense", new[] {
            "Адаптер питания","Фитнес-браслет","Удлинитель","Карта памяти","Видеокамера",
            "Вилка","Розетка","Выключатель","Кабель","Роутер","Батарейки","Светильник",
            "Фонарик","Крокодилы","Индикаторная отвёртка","Диск на болгарку","Миксер",
            "Холодильник","Игра","Плэйстэйшн-5","Шурупы","Газовая горелка","Зарядное устройство",
            "Дюбель","Блендер","Комплектующие","Ключ разводной","Пасатижи","Полотно для ножевки",
            "Фильтр для очистки воды","Фум лента","Круглогубцы","Нож","Прочее","Чайник","Фен","Кондиционер"
        });
        SeedCat(conn, "потери", "expense", new[] {
            "Разница курсов","Подгонка","Уменьшение лимита","Банк приворовывает",
            "Воровство в Велмарте","Судовой сбор","может потерял"
        });
        SeedCat(conn, "Игнатий", "expense", new[] {
            "Куриные лапы","Свиные кости","Шейки куриные","Корм сухой","Корм жидкий","Травка",
            "Печень","ветеринар","перевозка","Гигиенический наполнитель","Руковица для вычёсывания",
            "Игрушка","Филе курицы","Творог","Светильник","Когтеточка","Лоток","Шарик",
            "Полотенце","Совок","Капли против блох","Ошейник","Переноска","Миска","Шампунь"
        });
        SeedCat(conn, "Банки", "expense", new[] {
            "Монобанк","Ощад2","Приват","Ощадбанк","Аваль","Укрсиббанк","Сбербанк"
        });
        SeedCat(conn, "Компьютер", "expense", new[] {
            "ИИ","Аккумулятор","Интернет мастер","Веб-камера","Коврик для мышки",
            "ABBYY Screenshot Reader","Обжим кабеля","HomeBuh"
        });
        SeedCat(conn, "Услуги", "expense", new[] {
            "Туалет","Благотворительность","Почта","Ремонт техники"
        });
        SeedCat(conn, "Обувь", "expense", new[] {
            "Кроссовки","Ботинки"
        });
        SeedCat(conn, "Комиссия", "expense", new[] {
            "Подгонка","Обмен валюты","Перенос средств между счетами","Сдача билетов"
        });
        SeedCat(conn, "Развлечения", "expense", new[] {
            "Игра","Игрушка","Ёлка","Самокаты","Семена","Экскурсия"
        });
        SeedCat(conn, "Одежда", "expense", new[] {
            "Носки","Ремень","Кепка","Брюки","Рубашка"
        });
        SeedCat(conn, "Мебель", "expense", new[] {
            "Полка","Фурнитура","Постельное","Шведская стенка","Подушка"
        });
        SeedCat(conn, "Канцелярия", "expense", new[] {
            "Бумага","Увеличительное стекло","скобы к степлеру","Скотч","Степлер"
        });
        // legacy categories kept for backward compat
        SeedCat(conn, "Одежда и обувь", "expense", Array.Empty<string>());
        SeedCat(conn, "Связь",           "expense", Array.Empty<string>());
        SeedCat(conn, "Здоровье",        "expense", Array.Empty<string>());
        SeedCat(conn, "Прочее",          "expense", Array.Empty<string>());

        // ── Доходы ───────────────────────────────────────────────────────────
        SeedCat(conn, "Зарплата",            "income", Array.Empty<string>());
        SeedCat(conn, "Пенсия",             "income", Array.Empty<string>());
        SeedCat(conn, "Социальные выплаты", "income", Array.Empty<string>());
        SeedCat(conn, "Подарки",            "income", Array.Empty<string>());
        SeedCat(conn, "Прочие доходы",      "income", Array.Empty<string>());
    }

    private static void SeedCat(SqliteConnection conn, string cat, string type, string[] subs)
    {
        conn.Execute(
            "INSERT INTO categories(name,type) SELECT @n,@t WHERE NOT EXISTS (SELECT 1 FROM categories WHERE name=@n AND type=@t)",
            new { n = cat, t = type });
        foreach (var s in subs)
            conn.Execute(
                @"INSERT INTO subcategories(category_id,name)
                  SELECT c.id,@s FROM categories c WHERE c.name=@n AND c.type=@t
                  AND NOT EXISTS (SELECT 1 FROM subcategories x WHERE x.category_id=c.id AND x.name=@s)",
                new { n = cat, t = type, s });
    }
}
