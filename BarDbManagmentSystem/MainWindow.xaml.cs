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
                MessageBox.Show("Усі зміни успішно синхронізовано з Docker БД!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (DbUpdateException ex)
            {
                // Поліморфний виклик нашого кастомного обробника
                string userFriendlyMessage = ErrorHandler.HandleException(ex);

                MessageBox.Show(userFriendlyMessage, "Контроль констрейнтів СКБД", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Автоматичний Rollback (код залишається тим самим)
                RollbackChanges();
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

