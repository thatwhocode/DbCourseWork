using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace BarDbManagmentSystem
{
    public partial class ErrorWindow : Window
    {
        public int? selectedId { get; private set; } = null;
        private string _idPropertyName;
        private Type _referenceElementType;
        private IList _localList;

        public event Func<object, Task<int?>> OnRequestSaveReference;

 
        public ErrorWindow(string errorMessage, IEnumerable referernceData, string idPropertyName)
        {
            InitializeComponent();
            ErrorMessageText.Text = errorMessage;
            _idPropertyName = idPropertyName;

            _referenceElementType = referernceData.GetType().GetGenericArguments()[0];


            var castMethod = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(_referenceElementType);
            var castedResult = castMethod.Invoke(null, new object[] { referernceData });
            var toListMethod = typeof(Enumerable).GetMethod("ToList").MakeGenericMethod(_referenceElementType);
            _localList = (IList)toListMethod.Invoke(null, new object[] { castedResult });

            var observableType = typeof(ObservableCollection<>).MakeGenericType(_referenceElementType);
            ReferenceDataGrid.ItemsSource = (System.Collections.IEnumerable)Activator.CreateInstance(observableType, new object[] { _localList });

            HintText.Text = $"Оберіть рядок подвійним кліком щоб автоматично вставити значення {idPropertyName}";
            ReferenceDataGrid.MouseDoubleClick += ReferenceDataGrid_MouseDoubleClick;
        }

        private async void ReferenceDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItem = ReferenceDataGrid.SelectedItem;
            if (selectedItem == null) return;


            ReferenceDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ReferenceDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            this.Focus();

            PropertyInfo idProp = selectedItem.GetType().GetProperty(_idPropertyName);
            if (idProp == null) return;

            int currentId = Convert.ToInt32(idProp.GetValue(selectedItem));


            if (currentId == 0)
            {
                if (OnRequestSaveReference != null)
                {
                    HintText.Text = "⏳ Чекаємо відповіді від Docker СУБД...";
                    this.IsEnabled = false; // Блокуємо вікно від повторних кліків

                    // Передаємо чистий об'єкт наверх і чекаємо згенерований базою ID!
                    int? newGeneratedId = await OnRequestSaveReference.Invoke(selectedItem);

                    this.IsEnabled = true;

                    if (newGeneratedId.HasValue)
                    {
                        selectedId = newGeneratedId.Value;
                        this.DialogResult = true; // Успішно закриваємо вікно
                    }
                    else
                    {
                        HintText.Text = "❌ Помилка збереження. Перевірте введені дані в клітинках!";
                    }
                }
            }
            else
            {
                // Якщо обрали старого існуючого офіціанта
                selectedId = currentId;
                this.DialogResult = true;
            }
        }

        public void CreateNewReference_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                var newEntity = Activator.CreateInstance(_referenceElementType, true);
                if (newEntity == null) return;

                ReferenceDataGrid.IsReadOnly = false;

                //  Визначаємо через рефлексію властивостей об'єкта, чи є у нього текстові чи числові поля первинного ключа.
                // Оскільки контексту тут немає,  аналізуємо властивості безпосередньо за типами (string / int)
                var properties = newEntity.GetType().GetProperties();

                // Шукаємо потенційні поля ID або назви для багатьох-до-багатьох, щоб захистити від NULL
                foreach (var prop in properties)
                {
                    if (prop.Name.Equals(_idPropertyName, StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(long))
                        {
                            if (prop.CanWrite) prop.SetValue(newEntity, 0); // Тимчасовий дефолтний нуль для звичайних таблиць
                        }
                    }
                    // Якщо це таблиця Many-to-Many з текстовим ключем (наприклад, Languages або Specialization)
                    else if (prop.Name.Equals("Languages", StringComparison.OrdinalIgnoreCase) ||
                             prop.Name.Equals("Specialization", StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.PropertyType == typeof(string) && prop.CanWrite)
                        {
                            prop.SetValue(newEntity, string.Empty); // Захист від null первинного ключа
                        }
                    }
                }

                // 3. Додаємо суто в  локальний екранний список, база про це дізнається лише при подвійному кліку
                _localList.Add(newEntity);

                var observableType = typeof(ObservableCollection<>).MakeGenericType(_referenceElementType);
                var observabelCollection = Activator.CreateInstance(observableType, new object[] { _localList });

                ReferenceDataGrid.ItemsSource = (System.Collections.IEnumerable)observabelCollection;

                // 4. Налаштовуємо фокус на нову клітинку
                int targetColumnIndex = ReferenceDataGrid.Columns.Count > 1 ? 1 : 0;
                ReferenceDataGrid.SelectedItem = newEntity;
                ReferenceDataGrid.ScrollIntoView(newEntity);

                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ReferenceDataGrid.Columns.Count > targetColumnIndex)
                    {
                        var CellContent = ReferenceDataGrid.Columns[targetColumnIndex].GetCellContent(newEntity);
                        var cell = CellContent?.Parent as DataGridCell;
                        if (cell != null)
                        {
                            cell.Focus();
                            ReferenceDataGrid.BeginEdit();
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Input);

                MessageBox.Show("У таблицю знизу додано порожній рядок!\nВпишіть дані прямо в клітинки таблиці, натисніть Enter, а потім двічі клацніть на цей рядок для збереження.", "Швидке створення запису", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося ініціювати створення запису: {ex.Message}", "Помилка");
            }
        }
        public void ReferenceDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "DisplayName" ||
         e.PropertyName.EndsWith("Details") ||
         (e.PropertyType.IsGenericType && e.PropertyType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.ICollection<>)) ||
         (e.PropertyType.IsClass && e.PropertyType != typeof(string)))
            {
                e.Cancel = true;
            }
        }

        public void CloseButton_Click(object sender, RoutedEventArgs e) => this.DialogResult = false;
    }
}