using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Data.Constants;
using WebSosync.Enumerations;
using WebSosync.Models;

namespace WebSosync.Services
{
    public static class RequestValidator
    {
        public static object SosyncJobState { get; private set; }

        public static bool ValidateInterface(JobResultDto result, Dictionary<string, object> data)
        {
            var validationResult = true;

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

                validationResult = false;
            }

            // Warnings for fields that will be mandatory later

            CheckFieldExists("job_source_sosync_write_date", data, interfaceErrors);
            CheckFieldExists("job_source_fields", data, interfaceErrors);
            
            return validationResult;
        }

        public static bool ValidateData(JobResultDto result, Dictionary<string, object> data, string dateFormat)
        {
            var dataErrors = new Dictionary<string, string>();

            CheckDataEmpty("job_date", data, dataErrors);
            //CheckTimeFormat("job_date", data, dataErrors, dateFormat);
            CheckDataEmpty("job_source_system", data, dataErrors);
            CheckList("job_source_system", data, dataErrors, new List<string>() { "fs", "fso" });
            CheckDataEmpty("job_source_model", data, dataErrors);
            CheckDataEmpty("job_source_record_id", data, dataErrors);

            if (!dataErrors.ContainsKey("job_source_record_id"))
                CheckInteger("job_source_record_id", data, dataErrors, 1, null);

            if (data.ContainsKey("job_source_type"))
            {
                var type = ((string)data["job_source_type"] ?? "").ToLower();
                if (type == SosyncJobSourceType.MergeInto)
                {
                    CheckInteger("job_source_merge_into_id", data, dataErrors, 1, null);
                }
            }

            if (dataErrors.Count > 0)
            {
                result.ErrorCode = (int)JobErrorCode.DataError;
                result.ErrorText = JobErrorCode.DataError.ToString();
                result.ErrorDetail = dataErrors;

                return false;
            }

            return true;
        }

        private static void CheckFieldExists(string fieldName, Dictionary<string, object> data, Dictionary<string, string> errorList)
        {
            if (!data.ContainsKey(fieldName))
                errorList.Add(fieldName, $"Field {fieldName} not specified.");
        }

        private static void CheckDataEmpty(string fieldName, Dictionary<string, object> data, Dictionary<string, string> errorList)
        {
            if (data[fieldName] == null)
            {
                errorList.Add(fieldName, $"Field {fieldName} must not be empty.");
                return;
            }

            if (data[fieldName].GetType() == typeof(string) && string.IsNullOrEmpty((string)data[fieldName]))
            {
                errorList.Add(fieldName, $"Field {fieldName} must not be empty.");
                return;
            }
        }

        private static void CheckTimeFormat(string fieldName, Dictionary<string, object> data, Dictionary<string, string> errorList, string exactFormat)
        {
            var check = DateTime.TryParseExact(
                (string)data[fieldName],
                exactFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var t);

            if (!check)
                errorList.Add(fieldName, $"Field {fieldName} must have the format: {DateTime.Now.ToString(exactFormat, CultureInfo.InvariantCulture)}");
        }

        private static void CheckInteger(string fieldName, Dictionary<string, object> data, Dictionary<string, string> errorList, int? min, int? max)
        {
            bool check = false;
            long i = -1;

            if (data[fieldName].GetType() == typeof(string))
            {
                check = long.TryParse((string)data[fieldName], out i);
            }
            else if (data[fieldName].GetType() == typeof(int) || data[fieldName].GetType() == typeof(long))
            {
                check = true;
                i = (long)data[fieldName];
            }

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

        private static void CheckList(string fieldName, Dictionary<string, object> data, Dictionary<string, string> errorList, IList<string> possibleValues)
        {
            if (!possibleValues.Contains(data[fieldName]))
                errorList.Add(fieldName, $"Field {fieldName} must be one of the values [{string.Join(", ", possibleValues)}]");
        }
    }
}
