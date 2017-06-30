using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using WebSosync.Enumerations;

namespace WebSosync.Services
{
    public class RequestValidator<T>
    {
        #region Members
        private HttpRequest _request;
        private ModelStateDictionary _modelState;
        private Dictionary<string, List<Func<string, string>>> _customChecks;
        #endregion

        #region Properties
        public JobErrorCode ErrorCode { get; protected set; }
        public Dictionary<string, string> Errors { get; protected set; }
        #endregion

        #region Constructors
        public RequestValidator()
        {
            _customChecks = new Dictionary<string, List<Func<string, string>>>();

            ErrorCode = JobErrorCode.None;
            Errors = new Dictionary<string, string>();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Setup the request and the modelstate for validation.
        /// </summary>
        /// <param name="request">The request from the controller.</param>
        /// <param name="modelState">The modelstate from the controller.</param>
        public void Configure(HttpRequest request, ModelStateDictionary modelState)
        {
            _request = request;
            _modelState = modelState;
        }

        /// <summary>
        /// Adds a custom check for the given parameter to the validator.
        /// </summary>
        /// <param name="parameter">Expression to retrieve the property.</param>
        /// <param name="check">The function that handles value checking.</param>
        /// <returns>Returns itself for fluent API.</returns>
        public RequestValidator<T> AddCustomCheck<TProp>(Expression<Func<T, TProp>> propertySelector, Func<string, string> check)
        {
            // Gets the PropertyInfo from the linq-expression, this is to avoid using strings,
            // and because it enables the use of data contracts
            var property = ((PropertyInfo)((MemberExpression)propertySelector.Body).Member);
            var att = property.GetCustomAttribute<DataMemberAttribute>();

            string parameter = att != null ? att.Name : property.Name;

            if (!_customChecks.ContainsKey(parameter))
                _customChecks.Add(parameter, new List<Func<string, string>>());

            _customChecks[parameter].Add(check);
            return this;
        }

        public void Validate()
        {
            if (_request == null || _modelState == null)
                throw new InvalidOperationException($"Validation not configured, run {nameof(Configure)} and pass a Request and a ModelState.");

            var properties = typeof(T).GetProperties();

            foreach (var prop in properties)
            {
                var att = prop.GetCustomAttribute<DataMemberAttribute>();

                // If DataMemberAttribute was found, use that name, else use property name
                var name = att != null ? att.Name : prop.Name;

                if (CheckRequestParameter(name))
                    if (CheckEmpty(name))
                        if (CheckData(prop, name))
                            if (IncludeModelState(name))
                                RunCustomChecks(name);
            }
        }

        private bool CheckRequestParameter(string parameter)
        {
            if (!_request.Query.ContainsKey(parameter))
            {
                // Interface errors always overide data errors
                if (ErrorCode != JobErrorCode.InterfaceError)
                    ErrorCode = JobErrorCode.InterfaceError;

                Errors.Add(parameter, $"{parameter} is required.");

                return false;
            }

            return true;
        }

        private bool CheckEmpty(string parameter)
        {
            if (_request.Query[parameter] == "")
            {
                // Data errors only set the error code if there is none yet
                if (ErrorCode == JobErrorCode.None)
                    ErrorCode = JobErrorCode.DataError;

                Errors.Add(parameter, $"{parameter} cannot be empty.");

                return false;
            }

            return true;
        }

        private bool CheckData(PropertyInfo prop, string parameter)
        {
            Type actualType = Nullable.GetUnderlyingType(prop.PropertyType);

            if (actualType == null)
                actualType = prop.PropertyType;

            var converter = TypeDescriptor.GetConverter(actualType);

            if (converter != null)
            {
                try
                {
                    converter.ConvertFromString(_request.Query[parameter]);
                    return true;
                }
                catch (Exception)
                {
                    // Data errors only set the error code if there is none yet
                    if (ErrorCode == JobErrorCode.None)
                        ErrorCode = JobErrorCode.DataError;

                    Errors.Add(parameter, $"{parameter} could not be parsed as {actualType.Name}.");

                    return false;
                }
            }
            else
                throw new InvalidOperationException($"No converter found for type {actualType.Name}");
        }

        private bool IncludeModelState(string parameter)
        {
            var entry = _modelState[parameter];

            if (entry.Errors.Count > 0)
            {
                // Data errors only set the error code if there is none yet
                if (ErrorCode == JobErrorCode.None)
                    ErrorCode = JobErrorCode.DataError;

                foreach (var err in entry.Errors)
                {
                    Errors.Add(parameter, err.ErrorMessage);
                }

                return false;
            }

            return true;
        }

        private bool RunCustomChecks(string parameter)
        {
            if (!_customChecks.ContainsKey(parameter))
                return true;

            var val = _request.Query[parameter];
            var result = true;

            foreach (var check in _customChecks[parameter])
            {
                var errMsg = check(val);

                if (!string.IsNullOrEmpty(errMsg))
                {
                    // Data errors only set the error code if there is none yet
                    if (ErrorCode == JobErrorCode.None)
                        ErrorCode = JobErrorCode.DataError;

                    Errors.Add(parameter, errMsg);
                }

                // Chain all results with bitwise 'and'
                result = result & String.IsNullOrEmpty(errMsg);
            }

            return result;
        }
        #endregion
    }
}
