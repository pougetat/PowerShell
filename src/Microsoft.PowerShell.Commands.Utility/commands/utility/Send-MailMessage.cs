// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Management.Automation;
// using System.Net.Mail;
using System.Text;
using MailKit.Net.Smtp;
using MailKit;
using MimeKit;

namespace Microsoft.PowerShell.Commands
{
    public enum CompatMailPriority
    {
        Low,
        Normal,
        High
    }

    #region SendMailMessage
    /// <summary>
    /// Implementation for the Send-MailMessage command.
    /// </summary>
    [Cmdlet(VerbsCommunications.Send, "MailMessage", HelpUri = "https://go.microsoft.com/fwlink/?LinkID=135256")]
    public sealed class SendMailMessage : PSCmdlet
    {
        #region Command Line Parameters

        /// <summary>
        /// Gets or sets the files names to be attached to the email.
        /// If the filename specified can not be found, then the relevant error
        /// message should be thrown.
        /// </summary>
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("PsPath")]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        public string[] Attachments { get; set; }

        /// <summary>
        /// Gets or sets the address collection that contains the
        /// blind carbon copy (BCC) recipients for the e-mail message.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        public string[] Bcc { get; set; }

        /// <summary>
        /// Gets or sets the body (content) of the message.
        /// </summary>
        [Parameter(Position = 2, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether the mail message body is in Html.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [Alias("BAH")]
        public SwitchParameter BodyAsHtml { get; set; }

        /// <summary>
        /// Gets or sets the encoding used for the content of the body and also the subject.
        /// This is set to ASCII to ensure there are no problems with any email server.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [Alias("BE")]
        [ValidateNotNullOrEmpty]
        [ArgumentEncodingCompletionsAttribute]
        [ArgumentToEncodingTransformationAttribute]
        public Encoding Encoding { get; set; } = Encoding.ASCII;

        /// <summary>
        /// Gets or sets the address collection that contains the
        /// carbon copy (CC) recipients for the e-mail message.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Cc")]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        public string[] Cc { get; set; }

        /// <summary>
        /// Gets or sets the delivery notifications options for the e-mail message. The various
        /// options available for this parameter are None, OnSuccess, OnFailure, Delay and Never.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [Alias("DNO")]
        [ValidateNotNullOrEmpty]
        public DeliveryNotificationOptions DeliveryNotificationOption { get; set; }

        /// <summary>
        /// Gets or sets the from address for this e-mail message. The default value for
        /// this parameter is the email address of the currently logged on user.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string From { get; set; }

        /// <summary>
        /// Gets or sets the name of the Host used to send the email. This host name will be assigned
        /// to the Powershell variable PSEmailServer, if this host can not reached an appropriate error.
        /// message will be displayed.
        /// </summary>
        [Parameter(Position = 3, ValueFromPipelineByPropertyName = true)]
        [Alias("ComputerName")]
        [ValidateNotNullOrEmpty]
        public string SmtpServer { get; set; }

        /// <summary>
        /// Gets or sets the priority of the email message. The valid values for this are Normal, High and Low.
        /// This will have to be changed to NonUrgent, Normal, Urgent to match the new API specs
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public CompatMailPriority Priority { get; set; }

        /// <summary>
        /// Gets or sets the Reply-To field for this e-mail message.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public string[] ReplyTo { get; set; }

        /// <summary>
        /// Gets or sets the subject of the email message.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
        [Alias("sub")]
        [ValidateNotNullOrEmpty]
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the To address for this e-mail message.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        public string[] To { get; set; }

        /// <summary>
        /// Gets or sets the credential for this e-mail message.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [Credential]
        [ValidateNotNullOrEmpty]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Gets or sets if Secured layer is required or not.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public SwitchParameter UseSsl { get; set; }

        /// <summary>
        /// Gets or sets the Port to be used on the server. <see cref="SmtpServer"/>
        /// </summary>
        /// <remarks>
        /// Value must be greater than zero.
        /// </remarks>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateRange(0, Int32.MaxValue)]
        public int Port { get; set; }

        #endregion

        #region Private variables and methods

        // Instantiate a new instance of MailMessage
        private MimeMessage message = new MimeMessage();

        private SmtpClient smtpClient = null;

        /// <summary>
        /// Add the input addresses which are either string or hashtable to the MailMessage.
        /// It returns true if the from parameter has more than one value.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="param"></param>
        private void AddAddressesToMailMessage(object address, string param)
        {
            string[] objEmailAddresses = address as string[];
            foreach (string strEmailAddress in objEmailAddresses)
            {
                try
                {
                    switch (param)
                    {
                        case "to":
                            {
                                message.To.Add(new MailboxAddress(strEmailAddress));
                                break;
                            }
                        case "cc":
                            {
                                message.Cc.Add(new MailboxAddress(strEmailAddress));
                                break;
                            }
                        case "bcc":
                            {
                                message.Bcc.Add(new MailboxAddress(strEmailAddress));
                                break;
                            }
                        case "replyTo":
                            {
                                message.ReplyTo.Add(new MailboxAddress(strEmailAddress));
                                break;
                            }
                    }
                }
                catch (FormatException e)
                {
                    ErrorRecord er = new ErrorRecord(e, "FormatException", ErrorCategory.InvalidType, null);
                    WriteError(er);
                    continue;
                }
            }
        }

        #endregion

        #region Overrides

        /// <summary>
        /// BeginProcessing override.
        /// </summary>
        protected override void BeginProcessing()
        {
            try
            {
                // Set the sender address of the mail message
                message.From.Add(new MailboxAddress(From));
            }
            catch (FormatException e)
            {
                ErrorRecord er = new ErrorRecord(e, "FormatException", ErrorCategory.InvalidType, From);
                ThrowTerminatingError(er);
            }

            // Set the recipient address of the mail message
            AddAddressesToMailMessage(To, "to");

            // Set the BCC address of the mail message
            if (Bcc != null)
            {
                AddAddressesToMailMessage(Bcc, "bcc");
            }

            // Set the CC address of the mail message
            if (Cc != null)
            {
                AddAddressesToMailMessage(Cc, "cc");
            }

            // Set the Reply-To address of the mail message
            if (ReplyTo != null)
            {
                AddAddressesToMailMessage(ReplyTo, "replyTo");
            }

            // Set the delivery notification
            message.DeliveryNotificationOptions = DeliveryNotificationOption;

            // Set the subject of the mail message
            message.Subject = Subject;

            // Set the body of the mail message
            (message.Body as TextPart).SetText(Encoding, Body);

            // Set the subject and body encoding
            message.SubjectEncoding = Encoding;
            message.BodyEncoding = Encoding;
            // Set the format of the mail message body as HTML
            message.IsBodyHtml = BodyAsHtml;

            // Set the priority of the mail message to normal
            // We need to ensure compatibility with the deprecated System.Net.Mail
            // this cmdlet used to be implemented with
            if (Priority == CompatMailPriority.Low)
            {
                message.Priority = MessagePriority.NonUrgent;
            }
            if (Priority == CompatMailPriority.Normal)
            {
                message.Priority = MessagePriority.Normal;
            }
            if (Priority == CompatMailPriority.High)
            {
                message.Priority = MessagePriority.Urgent;
            }

            // Get the PowerShell environment variable
            // globalEmailServer might be null if it is deleted by: PS> del variable:PSEmailServer
            PSVariable globalEmailServer = SessionState.Internal.GetVariable(SpecialVariables.PSEmailServer);

            if (SmtpServer == null && globalEmailServer != null)
            {
                SmtpServer = Convert.ToString(globalEmailServer.Value, CultureInfo.InvariantCulture);
            }

            if (string.IsNullOrEmpty(SmtpServer))
            {
                ErrorRecord er = new ErrorRecord(new InvalidOperationException(SendMailMessageStrings.HostNameValue), null, ErrorCategory.InvalidArgument, null);
                this.ThrowTerminatingError(er);
            }

            if (Port == 0)
            {
                smtpClient = new SmtpClient(SmtpServer);
            }
            else
            {
                smtpClient = new SmtpClient(SmtpServer, Port);
            }

            if (UseSsl)
            {
                smtpClient.EnableSsl = true;
            }

            if (Credential != null)
            {
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = Credential.GetNetworkCredential();
            }
            else if (!UseSsl)
            {
                smtpClient.UseDefaultCredentials = true;
            }
        }

        /// <summary>
        /// ProcessRecord override.
        /// </summary>
        protected override void ProcessRecord()
        {
            // Add the attachments
            if (Attachments != null)
            {
                string filepath = string.Empty;
                foreach (string attachFile in Attachments)
                {
                    try
                    {
                        filepath = PathUtils.ResolveFilePath(attachFile, this);
                    }
                    catch (ItemNotFoundException e)
                    {
                        // NOTE: This will throw
                        PathUtils.ReportFileOpenFailure(this, filepath, e);
                    }

                    Attachment mailAttachment = new Attachment(filepath);
                    message.Attachments.Add(mailAttachment);
                }
            }
        }

        /// <summary>
        /// EndProcessing override.
        /// </summary>
        protected override void EndProcessing()
        {
            try
            {
                // Send the mail message
                smtpClient.Send(message);
            }
            catch (SmtpFailedRecipientsException ex)
            {
                ErrorRecord er = new ErrorRecord(ex, "SmtpFailedRecipientsException", ErrorCategory.InvalidOperation, smtpClient);
                WriteError(er);
            }
            catch (SmtpException ex)
            {
                if (ex.InnerException != null)
                {
                    ErrorRecord er = new ErrorRecord(new SmtpException(ex.InnerException.Message), "SmtpException", ErrorCategory.InvalidOperation, _mSmtpClient);
                    WriteError(er);
                }
                else
                {
                    ErrorRecord er = new ErrorRecord(ex, "SmtpException", ErrorCategory.InvalidOperation, smtpClient);
                    WriteError(er);
                }
            }
            catch (InvalidOperationException ex)
            {
                ErrorRecord er = new ErrorRecord(ex, "InvalidOperationException", ErrorCategory.InvalidOperation, smtpClient);
                WriteError(er);
            }
            catch (System.Security.Authentication.AuthenticationException ex)
            {
                ErrorRecord er = new ErrorRecord(ex, "AuthenticationException", ErrorCategory.InvalidOperation, smtpClient);
                WriteError(er);
            }

            // If we don't dispose the attachments, the sender can't modify or use the files sent.
            message.Attachments.Dispose();
        }

        #endregion
    }
    #endregion
}
