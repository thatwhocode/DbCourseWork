using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using BarDbManagmentSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BarDbManagmentSystem
{
    public static  class ErrorHandler
    {   public static string HandleException(DbUpdateException ex) {
            var failedEntry = ex.Entries.FirstOrDefault();
            if (failedEntry == null) { return $"Помилка БД{ex.InnerException?.Message ?? ex.Message}"; }
            var entity = failedEntry.Entity;
            string entityName = GetEntityDisplayName(entity);
            string SqlError = ex.InnerException?.Message ?? ex.Message;

            switch (failedEntry.State)
            {
                case EntityState.Deleted:
                    return ResolveDeleteConflict(entityName, SqlError);
                case EntityState.Added:
                case EntityState.Modified:
                    return ResolveUpdateConflict(entityName, SqlError);
                default:
                    return $"Обмеження СКБД заблокувало операцію{SqlError}";
            }
        }

        private static string ResolveUpdateConflict(string entityName, string sqlError)
        {
            if (sqlError.Contains("FOREIGN KEY") || sqlError.Contains("REFERENCE"))
            {
                return $"❌ ПОМИЛКА ЗОВНІШНЬОГО КЛЮЧА (Foreign Key)\n\n" +
                       $"Неможливо зберегти об'єкт [{entityName}].\n" +
                       $"Ви вказали ідентифікатор (ID) пов'язаної сутності, якого взагалі не існує в батьківській таблиці.\n\n" +
                       $"Рекомендація: Перевірте констрейнти та спочатку створіть запис у головній таблиці.";
            }

            if (sqlError.Contains("PRIMARY KEY") || sqlError.Contains("UNIQUE") || sqlError.Contains("duplicate key"))
            {
                return $" ПОМИЛКА УНІКАЛЬНОСТІ (Duplicate Key)\n\n" +
                       $"Запис у таблиці [{entityName}] містить ID або унікальне поле, яке вже зайняте іншим рядком у базі.\n\n" +
                       $"Рекомендація: Вкажіть інше унікальне значення.";
            }

            return $" Обмеження цілісності для [{entityName}]:\n{sqlError}";
        }
        

        private static string ResolveDeleteConflict(string entityName, string sqlError)
        {
            string dependentTable = "інших повязаних структурах";
            if (sqlError.Contains("Fk_"))
            {
                int fkIndex = sqlError.IndexOf("Fk");
                if (fkIndex != -1)
                {
                    string fkName = sqlError.Substring(fkIndex).Split(' ', '.', '"', ',')[0];
                    dependentTable = $"системному обмеженні '{fkName}'";
                }
            }
            return $" НЕМОЖЛИВО ВИДАЛИТИ ЗАПИС!\n\n" +
                   $"Об'єкт типу [{entityName}] не може бути видалений, оскільки на нього посилаються дані в {dependentTable}.\n\n" +
                   $"Рекомендація: Спочатку очистіть або переприв'яжіть залежні рядки.";
            
        }

        private static string GetEntityDisplayName(object entity)
        {
            if (entity is IDbEntityDisplay displayEntity) { 
                    return displayEntity.DisplayName;
            }
            return entity.GetType().Name;
        }
    }
}
