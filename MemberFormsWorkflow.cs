using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Forms.Core;
using Umbraco.Forms.Core.Attributes;
using Umbraco.Forms.Core.Enums;

namespace MemberFroms
{
    public class MemberFormsWorkflow : WorkflowType
    {
        #region Constructor & Helpers

        private readonly MemberFormsHelpers _memberFormsHelpers;

        public MemberFormsWorkflow()
        {
            this.Name = "Create Umbraco Member";
            this.Id = new Guid("3C7D3425-EA41-4DC2-B880-8A8FC5E97D68");
            this._memberFormsHelpers = new MemberFormsHelpers();
        }

        #endregion

        #region Member Properties & Relations

        [Umbraco.Forms.Core.Attributes.Setting("Name", description = "Select field for Name.", view = "textfield")]
        public string MemberName { get; set; }

        [Umbraco.Forms.Core.Attributes.Setting("Email", description = "Select field for Login and Email.", view = "textfield")]
        public string Email { get; set; }
        
        [Umbraco.Forms.Core.Attributes.Setting("Password", description = "Select field for Password.", view = "textfield")]
        public string Password { get; set; }

        [Umbraco.Forms.Core.Attributes.Setting("MemberType", description = "Select Member Type.", view = "dropdownlist")]
        public string MemberType { get; set; }

        [Umbraco.Forms.Core.Attributes.Setting("MemberGroup", description = "Select Member Group.", view = "dropdownlist")]
        public string MemberGroup { get; set; }

        #endregion

        #region Settings

        public override Dictionary<string, Setting> Settings()
        {
            var settings = base.Settings();

            settings["MemberType"].prevalues = string.Join(",", values: umbraco.cms.businesslogic.member.MemberType.GetAll.Select(p => p.Alias));
            settings["MemberGroup"].prevalues = string.Join(",", values: umbraco.cms.businesslogic.member.MemberGroup.GetAll.Select(p => p.Text));

            return settings;
        }

        public override List<Exception> ValidateSettings()
        {
            var exceptions = new List<Exception>();

            if (string.IsNullOrEmpty(value: this.MemberName))
                exceptions.Add(new Exception("Please enter field for Login."));

            if (string.IsNullOrEmpty(value: this.Email))
                exceptions.Add(new Exception("Please enter field for Email."));

            if (string.IsNullOrEmpty(value: this.MemberType))
                exceptions.Add(new Exception("Please select a MemberType."));

            return exceptions;
        }

        #endregion

        public override WorkflowExecutionStatus Execute(Record record, RecordEventArgs e)
        {
            try
            {
                // Member Properties
                var name = this._memberFormsHelpers.GetFieldValue<string>(record: record, caption: this.MemberName, required: true);
                var email = this._memberFormsHelpers.GetFieldValue<string>(record: record, caption: this.Email, required: true);
                var password = this._memberFormsHelpers.GetFieldValue<string>(record: record, caption: this.Password, required: false);
                
                // Member Relations
                var memberTypeAlias = this.MemberType;
                var memberGroupName = this.MemberGroup;

                if (!this._memberFormsHelpers.MemberExists(email: email) && this._memberFormsHelpers.EmailValidate(email: email))
                {
                    try
                    {
                        // Create new member with provided fields and membertype
                        var newMember = ApplicationContext.Current.Services.MemberService.CreateMember(
                            username: email,
                            email: email,
                            name: name,
                            memberTypeAlias: memberTypeAlias);

                        ApplicationContext.Current.Services.MemberService.Save(entity: newMember);

                        if (!string.IsNullOrEmpty(value: password))
                        {
                            // Assign password if provided
                            ApplicationContext.Current.Services.MemberService.SavePassword(newMember, password);
                        }

                        if (!string.IsNullOrEmpty(value: memberGroupName))
                        {
                            // If membergroup if provided
                            ApplicationContext.Current.Services.MemberService.AssignRole(
                                memberId: newMember.Id,
                                roleName: memberGroupName);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Unable to create new member " + ex.Message);
                    }
                }

                return WorkflowExecutionStatus.Completed;
            }
            catch (Exception exception)
            {
                LogHelper.Error<string>(message: exception.Message, exception: exception);
                return WorkflowExecutionStatus.Failed;
            }
        }
    }

    /// <summary>
    /// Helper class for MemberForms WorkflowType
    /// </summary>
    public class MemberFormsHelpers
    {
        /// <summary>
        /// Checks if a Member already exists with this email
        /// </summary>
        /// <param name="email">Email to check</param>
        public bool MemberExists(string email)
        {
            try
            {
                return (ApplicationContext.Current.Services.MemberService.GetByEmail(email: email) != null);
            }
            catch (Exception exception)
            {
                LogHelper.Error<string>(exception.Message, exception);
                throw new Exception(exception.Message);
            }
        }

        /// <summary>
        /// https://msdn.microsoft.com/en-us/library/01escwtf%28v=vs.110%29.aspx
        /// </summary>
        public bool EmailValidate(string email)
        {
            try
            {
                // Use IdnMapping class to convert Unicode domain names.
                email = Regex.Replace(
                    input: email,
                    pattern: @"(@)(.+)$",
                    evaluator: DomainMapper,
                    options: RegexOptions.None,
                    matchTimeout: TimeSpan.FromMilliseconds(200));

                // Return true if email is in valid e-mail format.
                return Regex.IsMatch(
                    input: email,
                    pattern: @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                             @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                    options: RegexOptions.IgnoreCase,
                    matchTimeout: TimeSpan.FromMilliseconds(250));
            }
            catch (Exception exception)
            {
                LogHelper.Error<string>(exception.Message, exception);
                return false;
            }
        }

        /// <summary>
        /// https://msdn.microsoft.com/en-us/library/01escwtf%28v=vs.110%29.aspx
        /// </summary>
        static string DomainMapper(Match match)
        {
            try
            {
                // IdnMapping class with default property values.
                var idn = new IdnMapping();

                var domainName = match.Groups[2].Value;
                domainName = idn.GetAscii(unicode: domainName);

                return match.Groups[1].Value + domainName;
            }
            catch (Exception exception)
            {
                LogHelper.Error<string>(exception.Message, exception);
                throw new Exception(exception.Message);
            }
        }

        /// <summary>
        /// Get field value from form based on caption
        /// </summary>
        /// <param name="record">Form record</param>
        /// <param name="caption">The caption to check</param>
        /// <param name="required">True trows an Eception if field is not provided, false returns null</param>
        /// <returns>The string value from the field based on provided caption</returns>
        public T GetFieldValue<T>(Record record, string caption, bool required)
        {
            try
            {
                if (record.RecordFields.Values.All(p => p.Field.Caption != caption))
                {
                    if (required)
                        throw new Exception("Required field is not provided!");
                    return ((T)Convert.ChangeType(null, typeof(T)));
                }
                var fieldValue = record.RecordFields.Values.First(p => p.Field.Caption == caption).ValuesAsString();
                return ((T)Convert.ChangeType(fieldValue, typeof(T)));
            }
            catch (Exception exception)
            {
                LogHelper.Error<string>(exception.Message, exception);
                throw new Exception(exception.Message);
            }
        }
    }
}
