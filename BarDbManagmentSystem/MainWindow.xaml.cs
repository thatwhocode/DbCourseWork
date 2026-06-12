using System.Collections;
using System.DirectoryServices.ActiveDirectory;
using System.Net.WebSockets;
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
                _context.ChangeTracker.Clear();
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

                        // Прив'язуємо саме готову ObservableCollection до  таблиці
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
            
                DynamicDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                DynamicDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                if (_currentModelType != null)
                {
                    var entityMetadata = _context.Model.FindEntityType(_currentModelType);
                    var primaryKeyProperties = entityMetadata?.FindPrimaryKey()?.Properties;
                    bool isCompositeKey = primaryKeyProperties != null && primaryKeyProperties.Count > 1;

                    if (isCompositeKey && DynamicDataGrid.ItemsSource is IList localList)
                    {
                        foreach (var item in localList)
                        {
                            // Якщо новий об'єкт ще не відстежується трекером — додаємо його як Added
                            if (_context.Entry(item).State == EntityState.Detached)
                            {
                                _context.Entry(item).State = EntityState.Added;
                            }
                        }
                    }
                }

                // 3. Спроба зберегти (якщо там StaffId = -1,  catch з DbUpdateException)
                _context.SaveChanges();
                MessageBox.Show("Усі зміни успішно синхронізовано з Docker БД!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (ex is DbUpdateException dbEx || (ex.InnerException is DbUpdateException innerDbEx && (dbEx = innerDbEx) != null))
                {
                    var resolution = ErrorHandler.HandleException(dbEx, _context);
                    if (resolution.NeedsAdjustion)
                    {
                        var failedEntity = dbEx.Entries.First().Entity;

                        ErrorWindow errWin = new ErrorWindow(resolution.ErrorMessage, resolution.ReferenceData, resolution.IdFieldName);
                        errWin.Owner = this;

                        // Асинхронне створення нової сутності-довідника
                        errWin.OnRequestSaveReference += async (newObjectToSave) =>
                        {
                            using (var tempContext = new BarDbContext())
                            {
                                try
                                {
                                    var props = newObjectToSave.GetType().GetProperties();
                                    foreach (var p in props)
                                    {
                                        if ((p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.ICollection<>)) ||
                                            (p.PropertyType.IsClass && p.PropertyType != typeof(string)))
                                        {
                                            if (p.CanWrite) p.SetValue(newObjectToSave, null);
                                        }
                                    }

                                    tempContext.Add(newObjectToSave);
                                    var entityMetadata = tempContext.Model.FindEntityType(newObjectToSave.GetType());
                                    var pkName = entityMetadata?.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name;
                                    if (pkName != null) tempContext.Entry(newObjectToSave).Property(pkName).IsTemporary = true;

                                    await tempContext.SaveChangesAsync();

                                    var idProp = newObjectToSave.GetType().GetProperty(resolution.IdFieldName);
                                    if (idProp != null) return Convert.ToInt32(idProp.GetValue(newObjectToSave));
                                }
                                catch (Exception tempEx)
                                {
                                    MessageBox.Show($"Помилка Docker СУБД:\n{tempEx.Message}", "Помилка СКБД", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                                return null;
                            }
                        };

                        if (errWin.ShowDialog() == true && errWin.selectedId.HasValue)
                        {
                            try
                            {
                                DynamicDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                                DynamicDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                                var entityMetadata = _context.Model.FindEntityType(failedEntity.GetType());
                                var primaryKeyProperties = entityMetadata?.FindPrimaryKey()?.Properties;
                                bool isCompositeKey = primaryKeyProperties != null && primaryKeyProperties.Count > 1;

                                if (isCompositeKey)
                                {

                                    var newLinkEntity = Activator.CreateInstance(failedEntity.GetType(), true);

                                    foreach (var prop in failedEntity.GetType().GetProperties())
                                    {
                                        if (prop.CanRead && prop.CanWrite && !prop.PropertyType.IsInterface &&
                                            (!prop.PropertyType.IsClass || prop.PropertyType == typeof(string)))
                                        {
                                            prop.SetValue(newLinkEntity, prop.GetValue(failedEntity));
                                        }
                                    }

                                    // Виставляємо обраний валідний ID офіціанта
                                    var targetProp = newLinkEntity.GetType().GetProperty(resolution.IdFieldName);
                                    targetProp?.SetValue(newLinkEntity, errWin.selectedId.Value);

                                    // Видаляємо старий зламаний рядок із трекера
                                    _context.Entry(failedEntity).State = EntityState.Detached;

                                    // Оновлюємо UI колекцію
                                    if (DynamicDataGrid.ItemsSource is IList localList)
                                    {
                                        int index = localList.IndexOf(failedEntity);
                                        if (index >= 0) localList[index] = newLinkEntity;
                                    }


                                    _context.Entry(newLinkEntity).State = EntityState.Added;
                                    failedEntity = newLinkEntity;
                                }
                                else
                                {
                                    _context.ChangeTracker.Clear();
                                    _context.Entry(failedEntity).State = EntityState.Modified;
                                    _context.Entry(failedEntity).Property(resolution.IdFieldName).CurrentValue = errWin.selectedId.Value;
                                }


                                var otherChangedEntries = _context.ChangeTracker.Entries()
                                    .Where(x => x.Entity != failedEntity && (x.State == EntityState.Added || x.State == EntityState.Modified))
                                    .ToList();

                                foreach (var entry in otherChangedEntries) entry.State = EntityState.Unchanged;

                                // Фінальний комміт у Docker
                                _context.SaveChanges();

                                foreach (var entry in otherChangedEntries) entry.State = EntityState.Added;

                                var collectionView = CollectionViewSource.GetDefaultView(DynamicDataGrid.ItemsSource);
                                collectionView?.Refresh();

                                MessageBox.Show("Дані успішно скориговано і збережено в Docker!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }
                            catch (Exception saveEx)
                            {
                                var errText = saveEx.InnerException?.Message ?? saveEx.Message;
                                MessageBox.Show($"Не вдалося виконати фінальний запис: {errText}", "Конфлікт", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        RollbackChanges();
                    }
                    else
                    {
                        MessageBox.Show(resolution.ErrorMessage, "Порушення обмежень СКБД", MessageBoxButton.OK, MessageBoxImage.Error);
                        RollbackChanges();
                    }
                }
                else
                {
                    // Якщо це інша помилка (наприклад, InvalidOperation через трекер ключів)
                    var realMessage = ex.InnerException?.Message ?? ex.Message;
                    MessageBox.Show($"Помилка валідації сутності трекером EF Core:\n{realMessage}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    RollbackChanges();
                }
            }
        }


        private void DynamicDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "DisplayName" ||
        e.PropertyName.EndsWith("Details") ||
        (e.PropertyType.IsClass && e.PropertyType != typeof(string)) ||
        (typeof(System.Collections.IEnumerable).IsAssignableFrom(e.PropertyType) && e.PropertyType != typeof(string)))
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
                var newEntity = Activator.CreateInstance(_currentModelType, true);
                if (newEntity == null) return;

                var entityMetadata = _context.Model.FindEntityType(_currentModelType);
                var primaryKeyProperties = entityMetadata?.FindPrimaryKey()?.Properties;
                bool isCompositeKey = primaryKeyProperties != null && primaryKeyProperties.Count > 1;

                if (primaryKeyProperties != null)
                {
                    foreach (var pkProp in primaryKeyProperties)
                    {
                        if (!isCompositeKey)
                        {
                            if (pkProp.ClrType == typeof(int) || pkProp.ClrType == typeof(long))
                            {
                                _context.Entry(newEntity).Property(pkProp.Name).IsTemporary = true;
                            }
                        }
                        else
                        {
                            var propInfo = newEntity.GetType().GetProperty(pkProp.Name);
                            if (propInfo != null && propInfo.CanWrite)
                            {
                                if (pkProp.ClrType == typeof(string))
                                {
                                    propInfo.SetValue(newEntity, string.Empty);
                                }
                                else if (pkProp.ClrType == typeof(int) || pkProp.ClrType == typeof(long))
                                {
                                    propInfo.SetValue(newEntity, -1); // Тимчасовий ID
                                }
                            }
                        }
                    }
                }

                if (isCompositeKey)
                {
                    if (DynamicDataGrid.ItemsSource is IList localList)
                    {
                        localList.Add(newEntity);
                    }
                }
                else
                {
                    _context.Entry(newEntity).State = EntityState.Added;
                }

                DynamicDataGrid.ScrollIntoView(newEntity);
                DynamicDataGrid.SelectedItem = newEntity;
                DynamicDataGrid.UpdateLayout();

                int targetColumnIndex = isCompositeKey ? 0 : (DynamicDataGrid.Columns.Count > 1 ? 1 : 0);
                if (DynamicDataGrid.Columns.Count > targetColumnIndex)
                {
                    var cell = DynamicDataGrid.Columns[targetColumnIndex].GetCellContent(newEntity)?.Parent as DataGridCell;
                    cell?.Focus();
                    DynamicDataGrid.BeginEdit();
                }
            }
            catch (Exception ex)
            {
                var realErr = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Не вдалося створити рядок:\n{realErr}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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

