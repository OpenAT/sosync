using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Enumerations;
using WebSosync.Models;

namespace WebSosync.Services
{
    public static class RequestValidator
    {
        public static bool ValidateInterface(JobResultDto result, Dictionary<string, string> data)
        {
            var interfaceErrors = new Dictionary<string, string>();

            CheckFieldExists("job_date", data, interfaceErrors);
            CheckFieldExists("job_source_system", data, interfaceErrors);
            CheckFieldExists("job_source_model", data, interfaceErrors);
            CheckFieldExists("job_source_record_id", data, interfaceErrors);

            if (interfaceErrors.Count > 0)
            {
                result.ErrorCode = (int)JobErrorCode.InterfaceError;
                result.ErrorText = JobErrorCode.InterfaceError.ToString();
                result.ErrorDetail = interfaceErrors;

                return false;
            }

            return true;
        }

        public static bool ValidateData(JobResultDto result, Dictionary<string, string> data, string dateFormat)
        {
            var dataErrors = new Dictionary<string, string>();

            CheckDataEmpty("job_date", data, dataErrors);
            CheckTimeFormat("job_date", data, dataErrors, dateFormat);
            CheckDataEmpty("job_source_system", data, dataErrors);
            CheckList("job_source_system", data, dataErrors, new List<string>() { "fs", "fso" });
            CheckDataEmpty("job_source_model", data, dataErrors);
            CheckDataEmpty("job_source_record_id", data, dataErrors);
            if (!dataErrors.ContainsKey("job_source_record_id"))
                CheckInteger("job_source_record_id", data, dataErrors, 1, null);

            if (dataErrors.Count > 0)
            {
                result.ErrorCode = (int)JobErrorCode.DataError;
                result.ErrorText = JobErrorCode.DataError.ToString();
                result.ErrorDetail = dataErrors;

                return false;
            }

            return true;
        }

        private static void CheckFieldExists(string fieldName, Dictionary<string, string> data, Dictionary<string, string> errorList)
        {
            if (!data.ContainsKey(fieldName))
                errorList.Add(fieldName, $"Field {fieldName} not specified.");
        }

        private static void CheckDataEmpty(string fieldName, Dictionary<string, string> data, Dictionary<string, string> errorList)
        {
            if (string.IsNullOrEmpty(data[fieldName]))
                errorList.Add(fieldName, $"Field {fieldName} must not be empty.");
        }

        private static void CheckTimeFormat(string fieldName, Dictionary<string, string> data, Dictionary<string, string> errorList, string exactFormat)
        {
            var check = DateTime.TryParseExact(
                data[fieldName],
                exactFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var t);

            if (!check)
                errorList.Add(fieldName, $"Field {fieldName} must have the format: {DateTime.Now.ToString(exactFormat, CultureInfo.InvariantCulture)}");
        }

        private static void CheckInteger(string fieldName, Dictionary<string, string> data, Dictionary<string, string> errorList, int? min, int? max)
        {
            var check = int.TryParse(
                data[fieldName],
                out var i);

            if (!check)
            {
                errorList.Add(fieldName, $"Field {fieldName} must be of type Int32.");
                return;
            }

            if (min.HasValue && i < min.Value)
                errorList.Add(fieldName, $"Field {fieldName} cannot be lower than {min.Value}.");

            if (max.HasValue && i > max.Value)
                errorList.Add(fieldName, $"Field {fieldName} cannot be greater than {max.Value}.");
        }

        private static void CheckList(string fieldName, Dictionary<string, string> data, Dictionary<string, string> errorList, IList<string> possibleValues)
        {
            if (!possibleValues.Contains(data[fieldName]))
                errorList.Add(fieldName, $"Field {fieldName} must be one of the values [{string.Join(", ", possibleValues)}]");
        }
    }
}
