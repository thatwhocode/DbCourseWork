using System.Collections;
using System.Printing;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BarDbManagmentSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace BarDbManagmentSystem
{
    public partial class MainWindow : Window
    {
        private object? _currentDBSet;
        private Type? _currentModelType;
        private readonly BarDbContext _context;
        public MainWindow()
        {

            InitializeComponent();
            _context = new BarDbContext();
            BuildDynamicMenu();
        }
        private void BuildDynamicMenu()
        {
            var DbSetProperties = typeof(BarDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));
            foreach (var prop in DbSetProperties)
            {
                TableMenu.Items.Add(prop.Name);
            }
        }
        private void TableMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TableMenu.SelectedItem == null) { return; }
            string tableName = TableMenu.SelectedItem.ToString();
            CurrenTableTitle.Text = $"Таблиця {tableName}";

            try
            {
                DynamicDataGrid.ItemsSource = null;
                var prop = typeof(BarDbContext).GetProperty(tableName);

                _currentDBSet = prop?.GetValue(_context);
                if (_currentDBSet != null)
                {
                    _currentModelType = prop.PropertyType.GetGenericArguments()[0];

                    // Динамічно викликаємо метод Load(dbSet)
                    var loadMethod = typeof(EntityFrameworkQueryableExtensions)
                        .GetMethods()
                        .First(m => m.Name == "Load" && m.GetParameters().Length == 1)
                        .MakeGenericMethod(_currentModelType);

                    loadMethod.Invoke(null, new[] { _currentDBSet });

                    //  Витягуємо властивість Local
                    var localProp = _currentDBSet.GetType().GetProperty("Local");
                    var localValue = localProp?.GetValue(_currentDBSet);

                    if (localValue != null)
                    {
                        //  Динамічно викликаємо метод ToObservableCollection() у локального буфера
                        var toObservableMethod = localValue.GetType()
                            .GetMethods()
                            .First(m => m.Name == "ToObservableCollection" && m.GetParameters().Length == 0);

                        var observableCollection = toObservableMethod.Invoke(localValue, null);

                        // Прив'язуємо саме готову ObservableCollection до нашої таблиці
                        DynamicDataGrid.ItemsSource = observableCollection as IEnumerable;
                    }
                }
            }
            catch (Exception ex)
            {
                var realMessage = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Помилка завантаження даних: {realMessage}", "Помилка");
            }

        }
        private void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _context.SaveChanges();
                MessageBox.Show("Зміни збережено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (DbUpdateException ex)
            {
                string innerMsg = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"[DEBUG LOG]:\n{innerMsg}", "Дебаг логу SQL Server");
                string upperMsg = innerMsg.ToUpper();
                if (innerMsg.Contains("FOREIGN KEY") || innerMsg.Contains("REFERENCE") || innerMsg.Contains("DELETE statement conflicted"))
                {
                    bool isDeleteError = upperMsg.Contains("THE DELETE STATEMENT CONFLICTED");
                    string customHint = upperMsg switch
                    {
                        // 1. Зв'язки з залами (Halls)
                        var msg when msg.Contains("FK_BARTABLES_HALL_ID") ||
                                     msg.Contains("FK_SHIFTHALLS_HALL_ID") ||
                                     msg.Contains("FK_STAFF_HALLS_HALL_ID") => isDeleteError
                            ? "Неможливо видалити зал! До нього ще прив'язані столи або закріплений персонал."
                            : "Вказаного [Hall_id] не існує! Спочатку внесіть цей зал у таблицю [Halls].",

                        // 2. Зв'язки з категоріями (Увага: тут враховано опечатку "MENUIEMS")
                        var msg when msg.Contains("FK_MENUIEMS_CATEGORY_ID") => isDeleteError
                            ? "Неможливо видалити категорію! У ній все ще є страви або напої в меню."
                            : "Вказаного [Category_id] не існує! Спочатку додайте категорію в таблицю [Categories].",

                        // 3. Зв'язки з позиціями меню (MenuItems)
                        var msg when msg.Contains("FK_ORDERDETAILS_ITEMS") ||
                                     msg.Contains("FK_RECIPES_MENUITEM") => isDeleteError
                            ? "Неможливо видалити позицію меню! Вона вже присутня у чеках замовлень або в рецептах."
                            : "Вказаного [Item_id] не існує! Такої страви чи напою немає в меню.",

                        // 4. Зв'язки з замовленнями (Orders)
                        var msg when msg.Contains("FK_ORDERDETAILS_ORDERS") => isDeleteError
                            ? "Неможливо видалити замовлення, поки всередині нього є додані страви/товари."
                            : "Вказаного [Order_id] не існує! Неможливо додати деталі до цього замовлення.",

                        // 5. Зв'язки з інгредієнтами (Ingredients)
                        var msg when msg.Contains("FK_RECIPES_INGREDIENT_ID") => isDeleteError
                            ? "Неможливо видалити інгредієнт! Він все ще використовується в рецептах технологічних карт."
                            : "Вказаного [Ingredient_id] не існує! Спочатку додайте його в таблицю [Ingredients].",

                        // 6. Зв'язки з робочими змінами (Shifts)
                        var msg when msg.Contains("FK_SHIFTHALLS_SHIFT_ID") ||
                                     msg.Contains("FK_STAFFSHIFTS_SHIFT_ID") => isDeleteError
                            ? "Неможливо видалити зміну! На неї вже призначені зали або співробітники."
                            : "Вказаного [Shift_id] не існує! Перевірте правильність ідентифікатора зміни.",

                        // 7. Зв'язки зі співробітниками (Staff)
                        var msg when msg.Contains("FK_ORDERS_STAFF_ID") ||
                                     msg.Contains("FK_SHIFTS_STAFF_ID") ||
                                     msg.Contains("FK_STAFFHALLS_STAFF_ID") ||
                                     msg.Contains("FK_STAFF_LANGUAGES_STAFF_ID") ||
                                     msg.Contains("FK_STAFFSHIFTS_STAFF_ID") ||
                                     msg.Contains("FK_STAFFSPECIALITIES_STAFF_ID") => isDeleteError
                            ? "Неможливо видалити співробітника! На нього оформлені замовлення, зміни, мови або спеціальності."
                            : "Вказаного [Staff_id] не існує! Зареєструйте спочатку співробітника в таблицю [Staff].",

                        _ => isDeleteError
                            ? "Неможливо видалити запис, оскільки на нього посилаються інші структури бази даних."
                            : "Перевірте правильність введених ідентифікаторів зовнішніх ключів."
                    };

                    MessageBox.Show($"Помилка цілісності даних\n {customHint}", "Контроль обмежень БД", MessageBoxButton.OK, MessageBoxImage.Hand);
                }
                else if (innerMsg.Contains("PRIMARY KEY") || innerMsg.Contains("Violation of UNIQUE KEY") || innerMsg.Contains("Cannot insert duplicate key"))
                {
                    MessageBox.Show("Спроба дублювання унікальності ідентифікатора!", "Помилка унікальності ключа", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($" Обмеження СКБД:\n{innerMsg}", "Повідомлення бази даних", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                RollbackChanges();
            }
            catch (InvalidOperationException ex)
            {
                string msg = ex.Message.ToUpper();

                if (msg.Contains("SEVERED") || msg.Contains("CHILD ENTITY SHOULD BE DELETED"))
                {
                    MessageBox.Show("❌ Неможливо видалити або змінити цей запис!\nВін є обов'язковою частиною іншої структури (наприклад, позицією в активному чеку або інгредієнтом у рецепті).\n\nСпочатку видаліть весь зв'язаний документ або налаштуйте каскадне видалення.",
                                    "Контроль бізнес-логіки", MessageBoxButton.OK, MessageBoxImage.Hand);
                }
                else
                {
                    MessageBox.Show($"Внутрішня помилка логіки EF:\n{ex.Message}", "Повідомлення системи", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                RollbackChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження {ex.Message}", "Помилка СКБД");
            }

        }

        private void DynamicDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if ((e.PropertyType.IsClass && e.PropertyType != typeof(string)) ||
            typeof(System.Collections.IEnumerable).IsAssignableFrom(e.PropertyType) && e.PropertyType != typeof(string))
            {

                e.Cancel = true;
            }
        }

        private void AddRowClick(object sender, RoutedEventArgs e)
        {
            if (_currentDBSet == null || _currentModelType == null)
            {
                MessageBox.Show("Спочатку оберіть таблицю!", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var newEntity = Activator.CreateInstance(_currentModelType);

                var AddMethod = _currentDBSet.GetType().GetMethod("Add", new[] { _currentModelType });
                AddMethod?.Invoke(_currentDBSet, new[] { newEntity });
                DynamicDataGrid.ScrollIntoView(newEntity);
                DynamicDataGrid.SelectedItem = newEntity;
                DynamicDataGrid.UpdateLayout();
                var cell = DynamicDataGrid.Columns[0].GetCellContent(newEntity)?.Parent as DataGridCell;
                cell?.Focus();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося створити рядок: {ex.Message}");
            }
        }

        private void DeleteRowClick(object sender, RoutedEventArgs e)
        {
            var selectedItem = DynamicDataGrid.SelectedItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Оберіть рядок в таблиці для видалення", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            var confirm = MessageBox.Show("Ви впевнені що хочете видалити цей рядок?", "Пдтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
            try
            {
                var removedMethod = _currentDBSet.GetType().GetMethod("Remove", new[] { selectedItem.GetType() });
                removedMethod?.Invoke(_currentDBSet, new[] { selectedItem });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка видалення", "Увага!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RollbackChanges()
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // 1. Повністю скидаємо фокус з DataGrid на саме вікно
                        this.Focus();

                        // 2. Відключаємо відображення
                        DynamicDataGrid.ItemsSource = null;

                        // 3. Чистимо контекст від невалідних сутностей
                        foreach (var entry in _context.ChangeTracker.Entries())
                        {
                            switch (entry.State)
                            {
                                case EntityState.Modified:
                                    entry.State = EntityState.Unchanged;
                                    break;
                                case EntityState.Added:
                                    entry.State = EntityState.Detached;
                                    break;
                                case EntityState.Deleted:
                                    entry.Reload();
                                    break;
                            }
                        }

                        // 4. Перезавантажуємо чисті дані
                        TableMenu_SelectionChanged(TableMenu, null);

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка під час відкату змін: {ex.Message}");
                    }
                },
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}

