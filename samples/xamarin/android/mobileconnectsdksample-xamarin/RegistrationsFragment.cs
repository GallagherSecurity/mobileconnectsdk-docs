using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Com.Gallagher.Security.Mobileaccess;

namespace mobileconnectsdksample_xamarin
{
    class RegistrationsFragment : Fragment, RegisterMobileCredentialDialogFragment.IInvitationDetailsListener
    {
        // *********************************************************************************
        // Get a reference to the MobileAccess shared instance
        // *********************************************************************************
        private readonly IMobileAccess m_mobileAccess = MobileAccessProvider.Instance;

        private MobileCredentialRecyclerViewAdapter m_adapter;

        public RegistrationsFragment() { }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            RetainInstance = true;

            View view = inflater.Inflate(Resource.Layout.fragment_mobilecredential_list, container, false);

            Context context = view.Context;

            // Wire up the RecyclerView
            m_adapter = new MobileCredentialRecyclerViewAdapter(this);
            // *********************************************************************************
            // Populate it with the current set of credentials
            // *********************************************************************************
            m_adapter.SetCredentials(m_mobileAccess.MobileCredentials);

            RecyclerView recyclerView = (RecyclerView)view.FindViewById(Resource.Id.credential_list);
            recyclerView.SetLayoutManager(new LinearLayoutManager(context));
            recyclerView.SetAdapter(m_adapter);

            FloatingActionButton fab = (FloatingActionButton)view.FindViewById(Resource.Id.add_credential_fab);
            fab.Click += (sender, args) =>
            {
                // When someone clicks our FAB we popup a dialog asking for manual registration details
                var dlg = new RegisterMobileCredentialDialogFragment
                {
                    Cancelable = false,
                    InvitationDetailsListener = this // callback from the dialog
                };

                dlg.Show(Activity.FragmentManager, "fragment_register_mobile_credential");
            };
            return view;
        }

        public void OnMobileCredentialClicked(IMobileCredential item)
        {
            new AlertDialog.Builder(Activity)
                .SetMessage("Are you sure you want to delete the credential for " + item.FacilityName)
                .SetPositiveButton("Yes", (sender, args) =>
                {
                    // *********************************************************************************
                    // Ask the Mobile Connect SDK to delete our credential
                    // *********************************************************************************
                    m_mobileAccess.DeleteMobileCredential(item, (credential, error) =>
                    {
                        if (error != null)
                        {
                            Toast.MakeText(Activity, $"Error {error.LocalizedMessage}", ToastLength.Long).Show();
                        }
                        else
                        {
                            Toast.MakeText(Activity, $"Deleted!", ToastLength.Short).Show();
                            m_adapter.SetCredentials(m_mobileAccess.MobileCredentials);
                        }
                    });

                })
                .SetNegativeButton("No", (IDialogInterfaceOnClickListener)null)
                .Show();
        }

        // When the manual registration details dialog closes, it will pass it's result here
        public void OnInvitationDetails(bool succeeded, string invitationCode, string serverHost)
        {
            if (succeeded && serverHost != null && invitationCode != null)
            {
                try
                {
                    // *********************************************************************************
                    // When doing manual registration we build the URI ourselves.
                    // Normally we'd expect the full URI to be passed to us e.g. from an email hyperlink,
                    // or from some other custom code (perhaps you pass the URI through your own web-service
                    // *********************************************************************************
                    var invitationUri = m_mobileAccess.ResolveInvitationUri(serverHost, invitationCode);

                    // *********************************************************************************
                    // Ask the Mobile Connect SDK to register our credential
                    // *********************************************************************************
                    m_mobileAccess.RegisterCredential(invitationUri,
                        onRegistrationCompleted: (credential, error) =>
                        {
                            if (error != null)
                            {
                                Toast.MakeText(Activity, $"Registration {error.LocalizedMessage}", ToastLength.Long).Show();
                            }
                            else
                            {
                                Toast.MakeText(Activity, $"Registered!", ToastLength.Short).Show();
                                m_adapter.SetCredentials(m_mobileAccess.MobileCredentials);
                            }
                        },
                        onAuthenticationTypeSelectionRequested: selector =>
                        {
                            // If the credential allows for second-factor authentication, then we need to ask
                            // the user which method they'd prefer; Fingerprint or PIN?
                            //noinspection ConstantConditions
                            new AlertDialog.Builder(Activity)
                                    .SetMessage("Please select second factor authentication type")
                                    .SetCancelable(false)
                                    .SetPositiveButton("Fingerprint", (sender, args) => selector.Select(true, SecondFactorAuthenticationType.Fingerprint))
                                    .SetNegativeButton("Passcode", (sender, args) => selector.Select(true, SecondFactorAuthenticationType.Pin))
                                    .SetNeutralButton("Cancel", (sender, args) => selector.Select(false, null))
                                    .Show();
                        });

                }
                catch (Java.Net.URISyntaxException e)
                {
                    e.PrintStackTrace();
                }
            }
        }

        class MobileCredentialRecyclerViewAdapter : RecyclerView.Adapter
        {
            readonly RegistrationsFragment m_parent;

            public MobileCredentialRecyclerViewAdapter(RegistrationsFragment parent) => m_parent = parent;

            // we take a copy of the credentials array to avoid possible bugs where the
            // recyclerview is in the middle of loading something while the SDK changes credentials
            // out from under it.
            private readonly List<IMobileCredential> m_credentials = new List<IMobileCredential>();
            
            public void SetCredentials(IEnumerable<IMobileCredential> credentials)
            {
                m_credentials.Clear();
                m_credentials.AddRange(credentials);
                NotifyDataSetChanged();
            }

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                View view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.row_mobilecredential, parent, false);
                return new ViewHolder(view);
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder recylcerViewHolder, int position)
            {
                var holder = (ViewHolder)recylcerViewHolder;
                holder.Item = m_credentials[position];
                holder.IdView.Text = m_credentials[position].FacilityId.ToString();
                holder.ContentView.Text = m_credentials[position].FacilityName;

                holder.View.Click += (sender, args) => m_parent.OnMobileCredentialClicked(holder.Item);
            }

            public override int ItemCount => m_credentials.Count;


            class ViewHolder : RecyclerView.ViewHolder
            {
                public readonly View View;
                public readonly TextView IdView;
                public readonly TextView ContentView;
                public IMobileCredential Item;

                public ViewHolder(View view) : base(view)
                {
                    View = view;
                    IdView = (TextView)view.FindViewById(Resource.Id.id);
                    ContentView = (TextView)view.FindViewById(Resource.Id.content);
                }
            }
        }
    } // end class RegistrationsFragment
}