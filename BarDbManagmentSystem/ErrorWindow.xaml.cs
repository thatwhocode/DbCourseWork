using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using System.Collections.ObjectModel;
namespace BarDbManagmentSystem
{
    public partial class ErrorWindow : Window
    {
        public int? selectedId { get; private set; } = null;
        private string _idPropertyName;
        private Type _referenceElementType;
        private DbContext _context;
        public ErrorWindow(string errorMessage, IEnumerable referernceData, string idPropertyName,  DbContext context)
        {
            InitializeComponent();
            ErrorMessageText.Text = errorMessage;
            _idPropertyName = idPropertyName;
            ReferenceDataGrid.ItemsSource = referernceData;
            _referenceElementType = referernceData.GetType().GetGenericArguments()[0];
            HintText.Text = $"Оберіть рядок подвійним кліком щоб автоматично вставити значення {idPropertyName}";
            ReferenceDataGrid.MouseDoubleClick += ReferenceDataGrid_MouseDoubleClick;
        }
        private void ReferenceDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            var selectedItem = ReferenceDataGrid.SelectedItem;
            if (selectedItem != null) {
                try
                {
                    ReferenceDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    _context.SaveChanges();
                    PropertyInfo prop = selectedItem.GetType().GetProperty(_idPropertyName);
                    if (prop != null)
                    {
                        selectedId = Convert.ToInt32(prop.GetValue(selectedItem));
                        this.DialogResult = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося зчитати ID: {ex.Message}", "Помилка рефлексії", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            } else {
                return;
            }
        }
        public void ReferenceDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e) {
            if (e.PropertyType.IsGenericType && e.PropertyType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.ICollection<>) || e.PropertyName.EndsWith("Details") || e.PropertyType.IsClass && e.PropertyType != typeof(string)) {

                e.Cancel = true;
            }
        }
        public void CloseButton_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
        }
        public void CreateNewReference_Click(object sender, RoutedEventArgs e) {
            try
            {
                var newEntity = Activator.CreateInstance(_referenceElementType);
                var DbSetMethodMethod = typeof(DbContext).GetMethod("Set", Type.EmptyTypes).MakeGenericMethod(_referenceElementType);
                var dbSet = DbSetMethodMethod.Invoke(_context, null);

                var addMethod = dbSet.GetType().GetMethod("Add", new[] { _referenceElementType });
                addMethod?.Invoke(dbSet, new[] { newEntity });

                ReferenceDataGrid.IsReadOnly = false;

                var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(_referenceElementType);
                var updatedList = (IEnumerable)toListMethod.Invoke(null, new object[] { dbSet });
                var observableType = typeof(ObservableCollection<>).MakeGenericType(_referenceElementType);
                var observabelCollection =Activator.CreateInstance(observableType, new object[] { updatedList});

                ReferenceDataGrid.ItemsSource = (System.Collections.IEnumerable)observabelCollection;

                ReferenceDataGrid.SelectedItem = newEntity;
                ReferenceDataGrid.ScrollIntoView(newEntity);
                ReferenceDataGrid.UpdateLayout();
                if (ReferenceDataGrid.Columns.Count() > 0) { 
                    var CellContent = ReferenceDataGrid.Columns[0].GetCellContent(newEntity);
                    var cell = CellContent?.Parent as DataGridCell;
                    if (cell != null) { 
                        cell.Focus();
                        ReferenceDataGrid.BeginEdit();
                    }
                }

                MessageBox.Show("У таблицю знизу додано порожній рядок!\nВпишіть дані  прямо в клітинки таблиці, натисніть Enter, а потім двічі клацніть на цей рядок для збереження.", "Швидке створення запису", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex){
                MessageBox.Show($"Не вдалося ініціювати створення запису: {ex.Message}");
            }
            }
    }
}
