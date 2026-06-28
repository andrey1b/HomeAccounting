namespace HomeAccounting.Services;

public static class AppLoc
{
    public static string Lang { get; private set; } = "ru";
    public static event Action? Changed;

    private static readonly Dictionary<string, string[]> D = new()
    {
        // Language menu
        ["menu_lang"]    = ["Мова",    "Язык",    "Language"],
        ["lang_ru"]      = ["Русский",    "Русский",    "Russian"],
        ["lang_en"]      = ["English",    "English",    "English"],

        // App / window titles
        ["app_title"]    = ["Домашній бюджет",    "Домашний бюджет",    "Home Budget"],
        ["tab_accounts"] = ["Рахунки",            "Счета",              "Accounts"],
        ["tab_expenses"] = ["Витрати",            "Расходы",            "Expenses"],
        ["tab_incomes"]  = ["Доходи",             "Доходы",             "Incomes"],

        // Import Preview Window
        ["menu_import_preview"]      = ["Імпорт xlsx (перегляд)…",    "Импорт xlsx (просмотр)…",     "Import xlsx (preview)…"],
        ["dlg_import_preview_title"] = ["Імпорт із xlsx (перегляд)",  "Импорт из xlsx (просмотр)",   "Import from xlsx (preview)"],
        ["lbl_imp_file"]             = ["Файл:",                       "Файл:",                       "File:"],
        ["btn_open_file"]            = ["Відкрити файл…",              "Открыть файл…",               "Open file…"],
        ["tab_imp_acc_brief"]        = ["Рахунки коротко",             "Счета кратко",               "Accounts (brief)"],
        ["tab_imp_detailed"]         = ["Рахунки докладно",            "Счета подробно",              "Accounts detailed"],
        ["col_init_balance"]         = ["Поч. баланс",                 "Нач. баланс",                 "Init. balance"],
        ["col_imp_expense"]          = ["Витрата,₴",                   "Расход,₴",                    "Expense,₴"],
        ["col_imp_income"]           = ["Дохід,₴",                     "Доход,₴",                     "Income,₴"],
        ["col_other_ops"]            = ["Прочие,₴",                    "Прочие,₴",                    "Other,₴"],
        ["col_turnover"]             = ["Оборот,₴",                    "Оборот,₴",                    "Turnover,₴"],
        ["lbl_imp_rows"]             = ["Рядків:",                     "Строк:",                      "Rows:"],
        ["btn_import"]               = ["Імпортувати",                 "Импортировать",               "Import"],
        ["btn_del_all"]              = ["Видалити всі",                "Удалить все",                 "Delete all"],
        ["msg_imp_no_rows"]          = ["Немає рядків для імпорту.",   "Нет строк для импорта.",      "No rows to import."],

        // Main menu
        ["menu_file"]            = ["Файл",                    "Файл",                     "File"],
        ["menu_exit"]            = ["Вихід",                   "Выход",                    "Exit"],
        ["menu_import_accounts"] = ["Завантажити xlsx → Рахунки...", "Загрузить xlsx → Счета...",   "Import xlsx → Accounts..."],
        ["menu_import_expenses"] = ["Завантажити xlsx → Витрати...", "Загрузить xlsx → Расходы...",  "Import xlsx → Expenses..."],
        ["menu_import_incomes"]  = ["Завантажити xlsx → Доходи...",  "Загрузить xlsx → Доходы...",   "Import xlsx → Incomes..."],
        ["menu_export_accounts"] = ["Зберегти Рахунки → xlsx...",    "Сохранить Счета → xlsx...",    "Export Accounts → xlsx..."],
        ["menu_export_expenses"] = ["Зберегти Витрати → xlsx...",    "Сохранить Расходы → xlsx...",  "Export Expenses → xlsx..."],
        ["menu_export_incomes"]  = ["Зберегти Доходи → xlsx...",     "Сохранить Доходы → xlsx...",   "Export Incomes → xlsx..."],
        ["msg_import_ok"]        = ["Імпортовано: {count} записів.", "Импортировано: {count} записей.", "Imported: {count} records."],
        ["msg_import_skip"]      = ["Пропущено: {skip} рядків.",     "Пропущено: {skip} строк.",        "Skipped: {skip} rows."],
        ["menu_refs"]     = ["Довідники",        "Справочники",        "References"],
        ["menu_cats_exp"] = ["Категорії витрат", "Категории расходов", "Expense categories"],
        ["menu_cats_inc"] = ["Категорії доходів","Категории доходов",  "Income categories"],
        ["menu_reports"]  = ["Звіти",            "Отчёты",             "Reports"],
        ["menu_maint"]       = ["Обслуговування",        "Обслуживание",              "Maintenance"],
        ["mi_open_folder"]   = ["Відкрити папку програми", "Открыть папку с программой","Open program folder"],
        ["mi_open_site"]     = ["Перейти на сайт програми","Перейти на сайт программы", "Go to program website"],
        ["mi_check_update"]  = ["Перевірити оновлення",    "Проверить обновления",       "Check for updates"],
        ["mi_clear_db"]      = ["Очистити БД…",             "Очистить БД…",               "Clear DB…"],
        ["mi_vacuum_db"]     = ["Стиснути БД",              "Сжать БД",                   "Compact DB"],

        // Buttons
        ["btn_add"]      = ["Додати",    "Добавить", "Add"],
        ["btn_edit"]     = ["Змінити",   "Изменить", "Edit"],
        ["btn_delete"]   = ["Видалити",  "Удалить",  "Delete"],
        ["btn_cancel"]   = ["Скасувати", "Отменить", "Cancel"],
        ["btn_show"]     = ["Показати",  "Показать", "Show"],
        ["btn_del_short"]= ["Видал.",    "Удал.",    "Del."],

        // Account tab
        ["chk_show_hidden"]      = ["Показати приховані",  "Показать скрытые",   "Show hidden"],
        ["lbl_total_balance"]    = ["Загальний баланс:",   "Общий баланс:",      "Total balance:"],
        ["lbl_receipt_account"]  = ["Рах. для чеків:",     "Счёт для чеков:",    "Account for receipts:"],
        ["none_option"]          = ["— немає —",           "— нет —",            "— none —"],

        // Filters
        ["lbl_from"]     = ["З:",        "С:",         "From:"],
        ["lbl_to"]       = ["По:",       "По:",        "To:"],
        ["lbl_account"]  = ["Рахунок:",  "Счёт:",     "Account:"],
        ["lbl_category"] = ["Категорія:","Категория:", "Category:"],
        ["all_filter"]   = ["— Всі —",  "— Все —",   "— All —"],

        // Summary bars
        ["lbl_today"]        = ["Сьогодні:",       "Сегодня:",         "Today:"],
        ["lbl_week"]         = ["Тиждень:",        "Неделя:",          "Week:"],
        ["lbl_month"]        = ["Місяць:",         "Месяц:",           "Month:"],
        ["lbl_filter_total"] = ["Всього у фільтрі:","Всего в фильтре:", "Filter total:"],

        // Column headers (no colon)
        ["col_num"]         = ["№",            "№",             "#"],
        ["col_name"]        = ["Назва",        "Название",      "Name"],
        ["col_account"]     = ["Рахунок",      "Счёт",         "Account"],
        ["col_note"]        = ["Примітка",     "Примечание",    "Note"],
        ["col_balance"]     = ["Баланс",       "Баланс",        "Balance"],
        ["col_date"]        = ["Дата",         "Дата",          "Date"],
        ["col_category"]    = ["Категорія",    "Категория",     "Category"],
        ["col_subcategory"] = ["Підкатегорія", "Подкатегория",  "Subcategory"],
        ["col_qty"]         = ["К-сть",        "Кол-во",        "Qty"],
        ["col_amount"]      = ["Сума",         "Сумма",         "Amount"],
        ["col_income_cat"]  = ["Категорія доходу",   "Категория дохода",   "Income category"],
        ["col_income_sub"]  = ["Підкатег. доходу",   "Подкатег. дохода",   "Income subcategory"],
        ["col_records"]     = ["Записів",      "Записей",       "Records"],

        // AccountDialog
        ["dlg_account_title"] = ["Рахунок",                    "Счёт",                          "Account"],
        ["dlg_account_add"]   = ["Додавання рахунку",          "Добавление счёта",              "Add Account"],
        ["dlg_account_edit"]  = ["Редагування рахунку",        "Редактирование счёта",          "Edit Account"],
        ["lbl_name"]          = ["Назва:",                     "Название:",                     "Name:"],
        ["lbl_init_balance"]  = ["Початковий баланс:",         "Начальный баланс:",             "Initial balance:"],
        ["lbl_icon"]          = ["Іконка рахунку:",            "Иконка счёта:",                 "Account icon:"],
        ["lbl_sort_order"]    = ["Порядковий номер:",          "Порядковый номер:",             "Sort order:"],
        ["lbl_currency"]      = ["Валюта:",                    "Валюта:",                       "Currency:"],
        ["chk_hidden"]        = ["Прихований",                 "Скрытый",                       "Hidden"],
        ["chk_hidden_acc"]    = ["Сховати з переліку рахунків","Скрыть из списка счетов",       "Hide from accounts list"],
        ["btn_hide"]          = ["Сховати",                    "Скрыть",                        "Hide"],
        ["col_icon"]          = ["Іконка",                     "Иконка",                        "Icon"],

        // ExpenseDialog / IncomeDialog
        ["dlg_expense_title"]  = ["Картка витрат",     "Карточка расходов", "Expense card"],
        ["dlg_income_title"]   = ["Картка доходів",    "Карточка доходов",  "Income card"],
        ["lbl_date"]           = ["Дата:",             "Дата:",             "Date:"],
        ["lbl_account_from"]   = ["Списати з рахунку:","Списать со счёта:", "Debit from:"],
        ["lbl_account_to"]     = ["Занести на рахунок:","Занести на счёт:", "Credit to:"],
        ["lbl_subcategory"]    = ["Підкатегорія:",     "Подкатегория:",     "Subcategory:"],
        ["lbl_qty"]            = ["Кількість:",        "Количество:",       "Quantity:"],
        ["lbl_amount"]         = ["Сума:",             "Сумма:",            "Amount:"],
        ["lbl_discount"]       = ["Знижка %:",         "Скидка %:",         "Discount %:"],

        // CategoryManagerWindow
        ["dlg_cats_expense_title"] = ["Категорії витрат",  "Категории расходов", "Expense categories"],
        ["dlg_cats_income_title"]  = ["Категорії доходів", "Категории доходов",  "Income categories"],
        ["lbl_categories"]         = ["Категорії",         "Категории",          "Categories"],
        ["lbl_subcategories"]      = ["Підкатегорії",      "Подкатегории",       "Subcategories"],

        // Filter checkbox
        ["chk_filter"] = ["Фільтр:", "Фильтр:", "Filter:"],

        // Accounts sub-tabs
        ["tab_acc_brief"]     = ["Кратко",    "Кратко",    "Overview"],
        ["tab_acc_detailed"]  = ["Докладно",  "Подробно",  "Detailed"],
        ["tab_acc_transfers"] = ["Переноси",  "Переносы",  "Transfers"],

        // Account columns
        ["col_expense"] = ["Витрати",  "Расход", "Expense"],
        ["col_income"]  = ["Дохід",    "Доход",  "Income"],
        ["col_unit"]    = ["Од. вим.", "Ед. изм.", "Unit"],

        // Transfer dialog
        ["dlg_transfer_title"]   = ["Переніс коштів",       "Перенос средств",        "Transfer"],
        ["lbl_from_account"]     = ["З рахунку:",            "Со счёта:",              "From account:"],
        ["lbl_to_account"]       = ["На рахунок:",           "На счёт:",               "To account:"],
        ["col_from_account"]     = ["З рахунку",             "Со счёта",               "From account"],
        ["col_to_account"]       = ["На рахунок",            "На счёт",                "To account"],
        ["lbl_tr_total"]         = ["Всього переносів:",     "Всего переносов:",       "Total transfers:"],

        // Validation for transfers
        ["msg_select_from_account"] = ["Оберіть рахунок-джерело.",     "Выберите счёт-источник.",    "Select source account."],
        ["msg_select_to_account"]   = ["Оберіть рахунок призначення.", "Выберите счёт назначения.", "Select target account."],
        ["msg_same_accounts"]       = ["Рахунки повинні відрізнятись.","Счета должны отличаться.",   "Accounts must differ."],

        // "More" button
        ["btn_more"] = ["+ Ще один", "+ Ещё один", "+ Add Another"],

        // ReportsWindow
        ["dlg_reports_title"] = ["Звіти",    "Отчёты",    "Reports"],
        ["rb_expenses"]       = ["Витрати",  "Расходы",   "Expenses"],
        ["rb_incomes"]        = ["Доходи",   "Доходы",    "Incomes"],
        ["lbl_total"]         = ["Всього:",  "Всего:",    "Total:"],
        ["lbl_records_lbl"]   = ["Записів:", "Записей:",  "Records:"],

        // Reports tabs
        ["tab_rpt_by_cat"]  = ["По категоріях", "По категориям", "By category"],
        ["tab_rpt_by_date"] = ["По датах",       "По датам",      "By date"],

        // Date grouping
        ["lbl_group_by"]   = ["Групувати:",   "Группировать:",  "Group by:"],
        ["grp_days"]       = ["По днях",      "По дням",        "By days"],
        ["grp_weeks"]      = ["По тижнях",    "По неделям",     "By weeks"],
        ["grp_months"]     = ["По місяцях",   "По месяцам",     "By months"],
        ["col_period"]     = ["Період",        "Период",         "Period"],

        // Phone install
        ["menu_install_phone"] = ["Встановити ComfortBuh на телефон...", "Установить ComfortBuh на телефон...", "Install ComfortBuh on phone..."],
        ["dlg_install_title"]  = ["Встановлення ComfortBuh на телефон", "Установка ComfortBuh на телефон", "Install ComfortBuh on phone"],
        ["dlg_install_step1"]  = ["Крок 1 — скануйте QR камерою телефона", "Шаг 1 — сканируйте QR камерой телефона", "Step 1 — scan QR with phone camera"],
        ["dlg_install_url"]    = ["або відкрийте в браузері телефону:", "или откройте в браузере телефона:", "or open in phone browser:"],
        ["dlg_install_hint"]   = ["Після встановлення скануйте QR ще раз — адреса ПК заповниться автоматично.",
                                   "После установки снова отсканируйте QR — адрес ПК заполнится автоматически.",
                                   "After install, scan QR again — PC address will be filled automatically."],

        // Load QR dialog
        ["menu_load_qr"]        = ["Завантажити QR-код...",    "Загрузить QR-код...",              "Load QR code..."],
        ["dlg_load_qr_title"]   = ["Завантажити QR-код чека",  "Загрузить QR-код чека",            "Load Receipt QR"],
        ["lbl_qr_url"]          = ["URL із QR-коду чека:",     "URL из QR-кода чека:",             "Receipt QR URL:"],
        ["btn_paste_text"]      = ["Вставити текст із буфера", "Вставить текст из буфера",         "Paste text"],
        ["btn_qr_clipboard"]    = ["QR-картинка з буфера",     "QR-картинка из буфера",            "QR image from clipboard"],
        ["btn_qr_file"]         = ["QR із файлу...",           "QR из файла...",                   "QR from file..."],
        ["btn_load_receipt"]    = ["Завантажити чек",          "Загрузить чек",                    "Load receipt"],
        ["msg_qr_no_url"]       = ["Введіть URL із QR-коду",  "Введите URL из QR-кода",           "Enter QR URL"],
        ["msg_qr_decoded"]      = ["QR-код розпізнано",        "QR-код успешно распознан",         "QR decoded"],
        ["msg_qr_decode_fail"]  = ["QR не розпізнано. Спробуйте інше зображення.", "QR не распознан. Попробуйте другое изображение.", "QR not recognized."],
        ["msg_loading_receipt"] = ["Завантажую чек з ДПС…",   "Загружаю чек из ДПС…",             "Loading receipt…"],

        // Receipt import
        ["msg_receipt_imported"] = ["Імпортовано {count} позицій з чека.", "Импортировано {count} позиций из чека.", "Imported {count} items from receipt."],
        ["msg_no_updates"]       = ["Програма актуальна. Оновлень немає.", "Программа актуальна. Обновлений нет.", "Program is up to date."],
        ["msg_receipt_error"]    = ["Помилка читання чека.",               "Ошибка чтения чека.",                   "Error reading receipt."],
        ["msg_receipt_no_items"] = ["У чеку не знайдено товарів.",         "В чеке не найдено товаров.",            "No items found in receipt."],
        ["status_watching"]      = ["Слідкую за чеками",                   "Слежу за чеками",                       "Watching receipts"],
        ["status_ready"]         = ["Готово",                              "Готово",                                "Ready"],

        // Validation / confirm messages
        ["msg_confirm"]               = ["Підтвердження",                                    "Подтверждение",                                "Confirmation"],
        ["msg_enter_name"]            = ["Введіть назву рахунку.",                           "Введите название счёта.",                      "Enter account name."],
        ["msg_invalid_balance"]       = ["Невірна сума початкового балансу.",                "Неверная сумма начального баланса.",           "Invalid initial balance."],
        ["msg_invalid_amount"]        = ["Введіть коректну суму.",                           "Введите корректную сумму.",                    "Enter a valid amount."],
        ["msg_select_account"]        = ["Оберіть рахунок.",                                 "Выберите счёт.",                               "Select an account."],
        ["msg_select_category"]       = ["Оберіть категорію.",                               "Выберите категорию.",                          "Select a category."],
        ["msg_select_cat_first"]      = ["Спочатку оберіть категорію.",                      "Сначала выберите категорию.",                  "Select a category first."],
        ["msg_confirm_del_account"]   = ["Видалити рахунок «{name}»?",                      "Удалить счёт «{name}»?",                       "Delete account «{name}»?"],
        ["msg_confirm_del"]           = ["Видалити запис?",                                  "Удалить запись?",                              "Delete record?"],
        ["msg_select_row"]            = ["Оберіть рядок у списку.",                          "Выберите строку в списке.",                    "Select a row in the list."],
        ["msg_confirm_del_cat"]       = ["Видалити категорію «{name}» та всі підкатегорії?", "Удалить категорию «{name}» и все подкатегории?","Delete category «{name}» and all subcategories?"],
        ["msg_confirm_del_subcat"]    = ["Видалити підкатегорію «{name}»?",                  "Удалить подкатегорию «{name}»?",               "Delete subcategory «{name}»?"],

        // ── v4.2.7: общее ───────────────────────────────────────────────────
        ["btn_ok"]        = ["OK", "OK", "OK"],
        ["col_currency"]  = ["Валюта", "Валюта", "Currency"],

        // Вход / пользователи
        ["login_title"]    = ["Вхід",                       "Вход",                          "Sign in"],
        ["login_header"]   = ["Домашній бюджет",            "Домашний бюджет",               "Home Budget"],
        ["login_user"]     = ["Користувач:",                "Пользователь:",                 "User:"],
        ["login_password"] = ["Пароль:",                    "Пароль:",                       "Password:"],
        ["login_enter"]    = ["Увійти",                     "Войти",                         "Sign in"],
        ["login_exit"]     = ["Вихід",                      "Выход",                         "Exit"],
        ["login_wrong"]    = ["Невірний пароль.",           "Неверный пароль.",              "Wrong password."],
        ["menu_user"]      = ["Користувач",                 "Пользователь",                  "User"],
        ["user_switch"]    = ["Змінити користувача…",       "Сменить пользователя…",         "Switch user…"],
        ["user_manage"]    = ["Керування користувачами…",   "Управление пользователями…",    "Manage users…"],
        ["user_current"]   = ["Користувач: {name}",         "Пользователь: {name}",          "User: {name}"],
        ["users_title"]    = ["Користувачі",                "Пользователи",                  "Users"],
        ["user_name"]      = ["Ім'я",                       "Имя",                           "Name"],
        ["user_haspass"]   = ["Пароль",                     "Пароль",                        "Password"],
        ["user_default"]   = ["За замовч.",                 "По умолч.",                     "Default"],
        ["user_set_pass"]  = ["Змінити пароль…",            "Изменить пароль…",              "Change password…"],
        ["user_set_default"]=["Зробити основним",           "Сделать основным",              "Make default"],
        ["user_new_pass"]  = ["Новий пароль (пусто = без пароля):", "Новый пароль (пусто = без пароля):", "New password (empty = none):"],

        // Меню «Облік» и новые справочники
        ["menu_accounting"]= ["Облік",                      "Учёт",                          "Accounting"],
        ["menu_budgets"]   = ["Бюджети…",                   "Бюджеты…",                      "Budgets…"],
        ["menu_debts"]     = ["Борги…",                     "Долги…",                        "Debts…"],
        ["menu_deposits"]  = ["Вклади…",                    "Вклады…",                       "Deposits…"],
        ["menu_currencies"]= ["Валюти та курси…",           "Валюты и курсы…",               "Currencies & rates…"],

        // Валюты / курсы
        ["currencies_title"]= ["Валюти та курси",           "Валюты и курсы",                "Currencies & rates"],
        ["tab_currencies"] = ["Валюти",                     "Валюты",                        "Currencies"],
        ["tab_rates"]      = ["Курси",                      "Курсы",                         "Rates"],
        ["cur_code"]       = ["Код",                        "Код",                           "Code"],
        ["cur_name"]       = ["Назва",                      "Название",                      "Name"],
        ["cur_symbol"]     = ["Символ",                     "Символ",                        "Symbol"],
        ["cur_default"]    = ["Основна",                    "Основная",                      "Default"],
        ["cur_make_default"]=["Зробити основною",           "Сделать основной",              "Make default"],
        ["rate_date"]      = ["Дата",                       "Дата",                          "Date"],
        ["rate_value"]     = ["Курс",                       "Курс",                          "Rate"],
        ["dlg_currency_title"]=["Валюта",                   "Валюта",                        "Currency"],
        ["dlg_rate_title"] = ["Курс валюти",                "Курс валюты",                   "Currency rate"],

        // Долги
        ["debts_title"]    = ["Борги",                      "Долги",                         "Debts"],
        ["debt_show"]      = ["Показати:",                  "Показать:",                     "Show:"],
        ["debt_all"]       = ["Всі",                        "Все",                           "All"],
        ["debt_debtor"]    = ["Мені винні",                 "Мне должны",                    "Owed to me"],
        ["debt_creditor"]  = ["Я винен",                    "Я должен",                      "I owe"],
        ["debt_kind"]      = ["Тип боргу:",                 "Тип долга:",                    "Debt type:"],
        ["debt_party"]     = ["Контрагент",                 "Контрагент",                    "Counterparty"],
        ["debt_percent"]   = ["Відсоток %:",               "Процент %:",                    "Percent %:"],
        ["debt_amount"]    = ["Сума",                       "Сумма",                         "Amount"],
        ["debt_back"]      = ["Повернуто",                  "Возвращено",                    "Returned"],
        ["debt_remaining"] = ["Залишок",                    "Остаток",                       "Remaining"],
        ["debt_status"]    = ["Статус",                     "Статус",                        "Status"],
        ["debt_closed"]    = ["Погашено",                   "Погашен",                       "Closed"],
        ["debt_date_close"]= ["Дата погашення:",           "Дата погашения:",               "Close date:"],
        ["debt_party_required"]=["Вкажіть контрагента.",   "Укажите контрагента.",          "Enter counterparty."],
        ["debt_total"]     = ["Мені винні: {owed}  •  Я винен: {iowe}", "Мне должны: {owed}  •  Я должен: {iowe}", "Owed to me: {owed}  •  I owe: {iowe}"],

        // Бюджеты
        ["budgets_title"]  = ["Бюджети",                    "Бюджеты",                       "Budgets"],
        ["lbl_year"]       = ["Рік:",                       "Год:",                          "Year:"],
        ["lbl_month"]      = ["Місяць:",                    "Месяц:",                        "Month:"],
        ["lbl_type"]       = ["Тип:",                       "Тип:",                          "Type:"],
        ["budget_plan"]    = ["План",                       "План",                          "Plan"],
        ["budget_fact"]    = ["Факт",                       "Факт",                          "Fact"],
        ["budget_diff"]    = ["Різниця",                    "Разница",                       "Difference"],
        ["budget_done"]    = ["Виконано, %",               "Выполнено, %",                  "Done, %"],
        ["budget_total"]   = ["План: {plan}  •  Факт: {fact}", "План: {plan}  •  Факт: {fact}", "Plan: {plan}  •  Fact: {fact}"],
        ["dlg_budget_title"]=["Бюджет",                     "Бюджет",                        "Budget"],
        ["budget_tab_exp"] = ["Бюджет витрат",             "Бюджет расходов",               "Expense budget"],
        ["budget_tab_inc"] = ["Бюджет доходів",            "Бюджет доходов",                "Income budget"],
        ["btn_today"]      = ["Сьогодні",                  "Сегодня",                       "Today"],
        ["btn_copy"]       = ["Копіювати",                 "Копировать",                    "Copy"],
        ["btn_print"]      = ["Друк",                      "Печать",                        "Print"],
        ["btn_export"]     = ["Експорт",                   "Экспорт",                       "Export"],
        ["budget_copy_title"]=["Копіювання бюджету",       "Копирование бюджета",           "Copy budget"],
        ["budget_copy_from"]=["Скопіювати бюджет з",       "Скопировать бюджет из",         "Copy budget from"],
        ["budget_copy_to"] = ["В",                         "В",                             "To"],
        ["budget_copy_exp"]= ["Копіювати бюджет витрат",   "Копировать бюджет расходов",    "Copy expense budget"],
        ["budget_copy_inc"]= ["Копіювати бюджет доходів",  "Копировать бюджет доходов",     "Copy income budget"],
        ["budget_copied"]  = ["Скопійовано планів: {count}", "Скопировано планов: {count}", "Plans copied: {count}"],

        // Скачивание курсов
        ["rate_download"]    = ["Завантажити курси валют",  "Скачать курсы валют",           "Download rates"],
        ["rate_downloaded"]  = ["Оновлено валют: {count} (на {date})", "Обновлено валют: {count} (на {date})", "Updated currencies: {count} ({date})"],
        ["rate_download_err"]= ["Не вдалося завантажити курси валют.", "Не удалось скачать курсы валют.", "Failed to download rates."],

        // Вклады
        ["deposits_title"] = ["Вклади",                     "Вклады",                        "Deposits"],
        ["dep_name"]       = ["Назва",                      "Название",                      "Name"],
        ["dep_amount"]     = ["Сума",                       "Сумма",                         "Amount"],
        ["dep_rate"]       = ["Ставка %",                   "Ставка %",                      "Rate %"],
        ["dep_open"]       = ["Відкрито",                   "Открыт",                        "Opened"],
        ["dep_close"]      = ["Закрито",                    "Закрыт",                        "Closed"],
        ["dlg_deposit_title"]=["Вклад",                     "Вклад",                         "Deposit"],
    };

    public static string T(string key)
    {
        if (!D.TryGetValue(key, out var arr)) return key;
        return Lang switch { "ru" => arr[1], "en" => arr[2], _ => arr[0] };
    }

    public static string T(string key, string placeholder, string value) =>
        T(key).Replace("{" + placeholder + "}", value);

    public static string T(string key, string p1, string v1, string p2, string v2) =>
        T(key).Replace("{" + p1 + "}", v1).Replace("{" + p2 + "}", v2);

    public static void SetLang(string lang)
    {
        if (lang == Lang) return;
        Lang = lang;
        Changed?.Invoke();
    }
}
