using Sitecore.Analytics.Tracking;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.SecurityModel;
using Sitecore.WFFM.Abstractions;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Analytics;
using Sitecore.WFFM.Abstractions.Shared;
using Sitecore.WFFM.Actions.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Wheelbarrowex.WFFM.SaveActions
{
    [Required("IsXdbEnabled", true), Required("IsXdbTrackerEnabled", true)]
    public class IdentifyAndAddContactToList : WffmSaveAction
    {
        private readonly IAnalyticsTracker analyticsTracker;

        private readonly IContactRepository contactRepository;

        public string ContactsLists
        {
            get;
            set;
        }

        public string EmialFieldName
        {
            get;
            private set;
        }

        public string FirstNameFieldName
        {
            get;
            private set;
        }

        public string ExecuteWhen
        {
            get;
            set;
        }

        public IdentifyAndAddContactToList(IAnalyticsTracker analyticsTracker, IContactRepository contactRepository, string emialFieldName, string firstNameFieldName)
        {
            Assert.IsNotNull(analyticsTracker, "analyticsTracker");
            Assert.IsNotNull(contactRepository, "contactRepository");
            Assert.IsNotNull(emialFieldName, "emialFieldName");
            Assert.IsNotNull(firstNameFieldName, "firstNameFieldName");
            this.analyticsTracker = analyticsTracker;
            this.contactRepository = contactRepository;
            EmialFieldName = emialFieldName;
            FirstNameFieldName = firstNameFieldName;
        }

        public override void Execute(ID formId, AdaptedResultList adaptedFields, ActionCallContext actionCallContext = null, params object[] data)
        {
            Assert.ArgumentNotNull(adaptedFields, "adaptedFields");
            Assert.IsNotNullOrEmpty(this.ContactsLists, "Empty contact list.");
            //Assert.IsNotNull(this.analyticsTracker.CurrentContact, "Tracker.Current.Contact");
            Log.Info("[Wheelbarrowex] Save action triggered", this);

            //identify the user if it is not
            if(this.analyticsTracker.CurrentContact.Identifiers.Identifier == null)
            {
                identifyCurrentUser(adaptedFields);
            }
            

                       //Now Let's add the contact to the list
            if (!adaptedFields.IsTrueStatement(this.ExecuteWhen))
            {
                return;
            }
            List<string> list = (from x in this.ContactsLists.Split(new char[]
            {
                ','
            })
                                 select ID.Parse(x).ToString()).ToList<string>();
            using (new SecurityDisabler())
            {
                Contact currentContact = this.analyticsTracker.CurrentContact;
                foreach (string current in list)
                {
                    currentContact.Tags.Set("ContactLists", current);
                }
                this.contactRepository.SaveContact(currentContact, true, null, new TimeSpan?(new TimeSpan(1000L)));
            }
        }

        private void identifyCurrentUser(AdaptedResultList adaptedFields)
        {
            //Select the email address from form input data
            var theEmail = (from x in adaptedFields
                            where x.FieldName == EmialFieldName
                            select x.Value).FirstOrDefault();
            //Identify the user using email - U can do with name as well but here we go with the email
            try
            {
                Sitecore.Analytics.Tracker.Current.Session.Identify(theEmail);
            }catch(Exception e)
            {
                Log.Error("[Wheelbarrow] User could not be identified", this);
                throw e;
            }
            

            var contact = Sitecore.Analytics.Tracker.Current.Session.Contact;

            // get the personal facet
            var contactPersonalInfo = contact.GetFacet<Sitecore.Analytics.Model.Entities.IContactPersonalInfo>("Personal");

            // set the contact's name
            contactPersonalInfo.FirstName = (from x in adaptedFields
                                             where x.FieldName == FirstNameFieldName
                                             select x.Value).FirstOrDefault();
            //add this part if u have surname
            //contactPersonalInfo.Surname = (from x in adaptedFields
            //                               where x.FieldName == "Last Name"
            //                               select x.Value).FirstOrDefault();

            // get the email facet
            var contactEmail = contact.GetFacet<Sitecore.Analytics.Model.Entities.IContactEmailAddresses>("Emails");

            // Create an email if not already present.
            // This can be named anything, but must be the same as "Preferred" if you want
            // this email to show in the Experience Profiles backend. 
            if (!contactEmail.Entries.Contains("Home"))
            {
                contactEmail.Entries.Create("Home");
            }

            // set the email
            var email = contactEmail.Entries["Home"];
            email.SmtpAddress = theEmail;
            contactEmail.Preferred = "Home";
            
        }
    }

}
