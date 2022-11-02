namespace GallagherUniversityStudentPortalSampleSite
{
    public class CommandCentreConfiguration
    {
        public CommandCentreConfiguration(
            string keyPersonalDataFieldId,
            string keyPersonalDataFieldName,
            string studentAccessGroupHref,
            string rootDivisionHref,
            string cardholderSearchHref,
            string mobileCredentialTypeHref)
        {
            KeyPersonalDataFieldId = keyPersonalDataFieldId;
            KeyPersonalDataFieldName = keyPersonalDataFieldName;
            StudentAccessGroupHref = studentAccessGroupHref;
            RootDivisionHref = rootDivisionHref;
            CardholderSearchHref = cardholderSearchHref;
            MobileCredentialTypeHref = mobileCredentialTypeHref;
        }

        // When searching for cardholders in command centre using a personal data field, we need the numeric
        // identifier of the personal data field itself. For example, the "Student ID" pdf may have an id of 500
        // So we need to use the "items" feature of the rest api to find that numeric id and store it for later use
        public string KeyPersonalDataFieldId { get; }
        public string KeyPersonalDataFieldName { get; }

        // When creating new cardholders we need to specify a Division and Access Group to associate them with
        // These are provided to the create cardholder call as href links to the item
        public string StudentAccessGroupHref { get; }
        public string RootDivisionHref { get; }

        // When searching for cardholders in command centre, we do a GET to a given URL.
        // In the current release of Command Centre, it's always '/api/cardholders'
        // However following the principles of REST and HATEOAS we get this URL by following links from the entrypoint.
        // This is also useful as if we're not privileged to load cardholders, the entrypoint won't have the
        // feature present, so we'll know that we're not allowed to search for cardholders before we try and start
        public string CardholderSearchHref { get; }

        // When adding mobile credentials to cardholders, we need to supply the "card type" href
        // Command Centre needs this to associate the credential with a "type" - the "type" has metadata such as
        // default enable/disable dates, card states and other things like that. Therefore we need to look it up 
        // by name and store the href for later use
        public string MobileCredentialTypeHref { get; }
    }
}
