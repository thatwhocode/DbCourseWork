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
                if (prop != null)
                {
                    var dbSet = prop.GetValue(_context);

                    if (dbSet != null)
                    {
                        if (dbSet is System.Collections.IEnumerable queryable)
                        {
                            var list = new System.Collections.ArrayList();
                            foreach (var item in queryable)
                            {
                                list.Add(item);
                            }

                            DynamicDataGrid.ItemsSource = list;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження даних: {ex.InnerException?.Message ?? ex.Message}", "Помилка");
            }
        }
        private void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _context.SaveChanges();
                MessageBox.Show("Зміни збережено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (DbUpdateException ex ){
                string innerMsg = ex.InnerException?.Message.ToLower() ?? ex.Message;
                if (innerMsg.Contains("foreign Key") || innerMsg.Contains("reference")) {
                    MessageBox.Show($"Помилка цілісності бази даних!\n Ви не ввели значення зовнішшнього ключа для таблиці{ex.TargetSite}", "Помилка констрейнту!", MessageBoxButton.OK, MessageBoxImage.Error);

                } else if (innerMsg.Contains("primary key") || innerMsg.Contains("violation of unique key")) {
                    MessageBox.Show("Спроба дублювання унікальності ідентифікатора!", "Помилка унікальності ключа", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (_currentDBSet == null || _currentModelType == null) {
                MessageBox.Show("Спочатку оберіть таблицю!", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        try{
            var newEntitty = Activator.CreateInstance(_currentModelType);

                var AddMethod = _currentDBSet.GetType().GetMethod("Add", new[] { _currentModelType});
                AddMethod?.Invoke(_currentDBSet, new[] { newEntitty });
                DynamicDataGrid.ScrollIntoView(newEntitty);

            }catch(Exception ex ){
                MessageBox.Show($"Не вдалося створити рядок: {ex.Message}");
        }
        }
        
        private void DeleteRowClick(object sender, RoutedEventArgs e)
        {
            var selectedItem = DynamicDataGrid.SelectedItem;
            if (selectedItem == null) {
                MessageBox.Show("Оберіть рядок в таблиці для видалення", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            var confirm = MessageBox.Show("Ви впевнені що хочете видалити цей рядок?", "Пдтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) {
                return;
            }
            try
            {
                var removedMethof = _currentDBSet.GetType().GetMethod("Remove", new[] { selectedItem.GetType() });
                removedMethof?.Invoke(_currentDBSet, new[] { selectedItem });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Помилка видалення", "Увага!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RollbackChanges() {
            foreach (var entry in _context.ChangeTracker.Entries()) {
                switch (entry.State) {
                    case EntityState.Modified:
                        entry.State = EntityState.Unchanged; break;
                    case EntityState.Added:
                        entry.State = EntityState.Detached; break;
                    case EntityState.Deleted:
                        entry.Reload(); break;
                }
                TableMenu_SelectionChanged(null!, null!);
            }
        }
    }
}