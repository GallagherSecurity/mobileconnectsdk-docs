using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Com.Gallagher.Security.Mobileaccess;
using Java.Lang;

namespace mobileconnectsdksample_xamarin
{
    // Java uses interfaces with a single method to represent lambda's. C# doesn't, so we add that functionality back where neccessary using extension methods
    static class InterfaceWrappers
    {
        class AnonymousCredentialDeleteListener : Java.Lang.Object, ICredentialDeleteListener
        {
            readonly Action<IMobileCredential, Throwable> m_handler;
            public AnonymousCredentialDeleteListener(Action<IMobileCredential, Throwable> handler) => m_handler = handler;
            public void OnCredentialDeleteCompleted(IMobileCredential p0, Throwable p1) => m_handler(p0, p1);
        }

        public static void DeleteMobileCredential(this IMobileAccess mobileAccess, IMobileCredential credential, Action<IMobileCredential, Throwable> onCredentialDeleteCompleted)
            => mobileAccess.DeleteMobileCredential(credential, new AnonymousCredentialDeleteListener(onCredentialDeleteCompleted));

        class AnonymousRegisterCredentialListener : Java.Lang.Object, IRegistrationListener
        {
            readonly Action<IMobileCredential, RegistrationError> m_onRegistrationCompleted;
            readonly Action<ISecondFactorAuthenticationTypeSelector> m_onAuthenticationTypeSelectionRequested;

            public AnonymousRegisterCredentialListener(Action<IMobileCredential, RegistrationError> onRegistrationCompleted, Action<ISecondFactorAuthenticationTypeSelector> onAuthenticationTypeSelectionRequested)
            {
                m_onRegistrationCompleted = onRegistrationCompleted;
                m_onAuthenticationTypeSelectionRequested = onAuthenticationTypeSelectionRequested;
            }

            public void OnAuthenticationTypeSelectionRequested(ISecondFactorAuthenticationTypeSelector p0) => m_onAuthenticationTypeSelectionRequested(p0);
            public void OnRegistrationCompleted(IMobileCredential p0, RegistrationError p1) => m_onRegistrationCompleted(p0, p1);
        }

        public static void RegisterCredential(
            this IMobileAccess mobileAccess,
            Java.Net.URI uri,
            Action<IMobileCredential, RegistrationError> onRegistrationCompleted,
            Action<ISecondFactorAuthenticationTypeSelector> onAuthenticationTypeSelectionRequested)
            => mobileAccess.RegisterCredential(uri, new AnonymousRegisterCredentialListener(onRegistrationCompleted, onAuthenticationTypeSelectionRequested));
    }
}