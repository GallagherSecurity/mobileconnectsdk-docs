using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace mobileconnectsdksample_xamarin
{
    class RegisterMobileCredentialDialogFragment : DialogFragment
    {
        public interface IInvitationDetailsListener
        {
            void OnInvitationDetails(bool succeeded, string invitationCode, string serverHost);
        }

        public IInvitationDetailsListener InvitationDetailsListener { get; set; }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
            => inflater.Inflate(Resource.Layout.fragment_register_mobile_credential, container);

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            var okButton = view.FindViewById<Button>(Resource.Id.ok_button);
            var cancelButton = view.FindViewById<Button>(Resource.Id.cancel_button);
            var invitationCodeText = view.FindViewById<EditText>(Resource.Id.invitation_code);
            var serverHostText = view.FindViewById<EditText>(Resource.Id.server_url);

            // Convenience for manual registration - if the clipboard looks like it has an invitation code in it, paste that in
            if(Activity.GetSystemService(Context.ClipboardService) is ClipboardManager clipboard)
            {
                ClipData clip = clipboard.PrimaryClip;
                if (clip != null && clip.ItemCount > 0)
                {
                    var clipText = clip.GetItemAt(0).CoerceToText(Activity);
                    if (clipText != null && Regex.IsMatch(clipText, "\\w{4}-\\w{4}-\\w{4}-\\w{4}"));
                    { // looks like an invitation code
                        invitationCodeText.Text = clipText;
                    }
                }
            }

            okButton.Click += (sender, args) =>
            {
                InvitationDetailsListener?.OnInvitationDetails(true, invitationCodeText.Text, serverHostText.Text);
                Dismiss();
            };

            cancelButton.Click += (sender, args) =>
            {
                InvitationDetailsListener?.OnInvitationDetails(false, null, null);
                Dismiss();
            };
        }
    }
}