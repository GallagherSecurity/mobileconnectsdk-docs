using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Com.Gallagher.Security.Mobileaccess;

namespace mobileconnectsdksample_xamarin
{
    class ReadersFragment : Fragment, ISdkStateListener, IAutomaticAccessListener
    {
        const int PermissionRequestCoarseLocation = 1;

        enum ReaderVisualState
        {
            None,
            Connecting,
            Granted,
            Denied
        }

        // "ViewModel" to render our reader information along with connection state
        private class ReaderWithVisualState
        {
            public IReaderAttributes Reader;
            public ReaderVisualState VisualState;

            public ReaderWithVisualState(IReaderAttributes reader, ReaderVisualState visualState)
            {
                Reader = reader;
                VisualState = visualState;
            }
        }

        // *********************************************************************************
        // Get a reference to the MobileAccess shared instance
        // *********************************************************************************
        private readonly IMobileAccess mMobileAccess = MobileAccessProvider.Instance;

        ReaderRecyclerViewAdapter mAdapter;

        public ReadersFragment() { }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_reader_list, container, false);
            var context = view.Context;

            // Wire up the RecyclerView
            mAdapter = new ReaderRecyclerViewAdapter(this);

            // *********************************************************************************
            // Ask the SDK to tell us about it's operational state so we can show warning messages if needed
            // *********************************************************************************
            mMobileAccess.AddSdkStateListener(this);

            // *********************************************************************************
            // Ask the SDK to tell us about readers it discovers
            // *********************************************************************************
            mMobileAccess.SetReaderUpdateListener(mAdapter);

            // *********************************************************************************
            // Ask the SDK to tell us about automatic access so we can show UI if needed
            // *********************************************************************************
            mMobileAccess.AddAutomaticAccessListener(this);

            var recyclerView = view.FindViewById<RecyclerView>(Resource.Id.reader_list);
            recyclerView.SetLayoutManager(new LinearLayoutManager(context));
            recyclerView.SetAdapter(mAdapter);

            return view;
        }

        public override void OnDestroy()
        {
            mMobileAccess.RemoveAutomaticAccessListener(this);
            base.OnDestroy();
        }

        // *********************************************************************************
        // Manually request access for the given reader
        // *********************************************************************************
        public void OnReaderClicked(IReader reader) => mMobileAccess.RequestAccess(reader, this);

        // *********************************************************************************
        // SdkStateListener:
        // The MobileConnect SDK will publish the list of problems/warnings via this callback.
        // so we can use it to show warning messages and things like that
        // *********************************************************************************
        public void OnStateChanged(bool isScanning, ICollection<MobileAccessState> states)
        {
            var messages = new List<string>();

            foreach (MobileAccessState state in states)
            {
                // can't switch on a java enum from C#
                if (state == MobileAccessState.ErrorNoCredentials)
                {
                    messages.Add("Please register a credential");
                }
                else if (state == MobileAccessState.NoNfcFeature)
                {
                    // A reasonable subset of Android phones do not support NFC, so it is likely
                    // you don't want to warn about lack of NFC; They can just use Bluetooth instead
                    messages.Add("This device does not support NFC");
                }
                else if (state == MobileAccessState.BleErrorDisabled)
                {
                    messages.Add("Bluetooth is disabled; Please enable it to allow access using Bluetooth");
                }
                else if (state == MobileAccessState.NfcErrorDisabled)
                {
                    messages.Add("NFC is disabled; Please enable it to allow access using NFC");
                }
                else if (state == MobileAccessState.BleErrorNoLocationPermission)
                {
                    messages.Add("Please grant permission for this application to use your location.");
                    // Request location permissions from user.
                    // It's recommended you do this in a more sensible place so as not to spam the user with requests
                    if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.M)
                    {
                        RequestPermissions(new string[] { Manifest.Permission.AccessCoarseLocation }, PermissionRequestCoarseLocation);
                    }
                }
                else if (state == MobileAccessState.BleErrorLocationServiceDisabled)
                {
                    messages.Add("Location services are disabled; Please enable them to allow bluetooth connectivity.");
                }
                else if (state == MobileAccessState.ErrorNoBleFeature)
                {
                    // This isn't likely to happen;
                    // Even ultra cheap $20 android devices released 5 years ago tend to  have bluetooth
                    messages.Add("This device does not support bluetooth");
                }
                else if (state == MobileAccessState.ErrorUnsupportedOsVersion)
                {
                    // The Mobile Connect SDK requires android version 5 or newer
                    // You can use the google play store to limit installation
                    // of your app to android 5+ to avoid this ever happening
                    messages.Add("Your device operating system version is not supported");
                    break;
                }
            }
            mAdapter.SetMessages(messages);
        }

        // *********************************************************************************
        // AutomaticAccessListener:
        // The MobileConnect SDK is telling us we need to put the phone back next to the reader
        // in order to complete an NFC transaction.
        // *********************************************************************************
        public void OnReturnToReaderRequired(IReader p0)
        { }  // This is handled at the Application level so we can ignore it

        // *********************************************************************************
        // AutomaticAccessListener:
        // *********************************************************************************
        public void OnReturnedToReader(IReader p0)
        { }  // This is handled at the Application level so we can ignore it


        // *********************************************************************************
        // AutomaticAccessListener (which is also the AccessListener for manual connect requests):
        // The MobileConnect SDK is telling us access is in progress for the given reader
        // so we may update the appropriate UI for that reader (e.g. show an animation)
        // *********************************************************************************
        public void OnAccessStarted(IReader reader) => mAdapter.SetReaderVisualState(reader, ReaderVisualState.Connecting);

        // *********************************************************************************
        // AutomaticAccessListener:
        // The MobileConnect SDK is telling us access completed for the given reader
        // *********************************************************************************
        public void OnAccessCompleted(IReader reader, IAccessResult accessResult, ReaderConnectionError error)
        {
            // 'error' only occurs if there's some sort of lower-level error (e.g. bluetooth disconnect)
            // in the normal case error will be null, and you should check accessResult.isAccessGranted().
            // accessResult.getAccessDecision() is the actual specific result behind the scenes
            if (accessResult != null && accessResult.IsAccessGranted)
            {
                mAdapter.SetReaderVisualState(reader, ReaderVisualState.Granted);
            }
            else
            {
                mAdapter.SetReaderVisualState(reader, ReaderVisualState.Denied);
            }

            // The SDK doesn't give us any more callbacks after accessComplete
            // so set a timer to clear the visual state after a small delay.
            new Handler(Looper.MainLooper).PostDelayed(() => mAdapter.SetReaderVisualState(reader, ReaderVisualState.None), 1000);
        }

        class ReaderRecyclerViewAdapter : RecyclerView.Adapter, IReaderUpdateListener
        {
            private readonly ReadersFragment m_parent;

            private readonly List<string> m_messages = new List<string>();
            private readonly List<ReaderWithVisualState> m_readers = new List<ReaderWithVisualState>();

            public ReaderRecyclerViewAdapter(ReadersFragment parent) => m_parent = parent;

            public void SetMessages(IEnumerable<string> messages)
            {
                m_messages.Clear();
                m_messages.AddRange(messages);
                NotifyDataSetChanged();
            }

            // *********************************************************************************
            // Updates to a reader will have different attributes and therefore
            // it doesn't really make sense to compare them directly. Instead we search for
            // "matching" readers with the same ID
            // *********************************************************************************
            public int FindReaderIndex(IReader reader)
            {
                for (int i = 0; i < m_readers.Count; i++)
                {
                    if (m_readers[i].Reader.Id == reader.Id)
                    {
                        return i;
                    }
                }
                return -1;
            }

            public void SetReaderVisualState(IReader reader, ReaderVisualState visualState)
            {
                int idx = FindReaderIndex(reader);
                if (idx >= 0)
                {
                    m_readers[idx].VisualState = visualState;
                    NotifyItemRangeChanged(idx, 1);
                }
                // if we don't find the reader, ignore it.
                // The SDK will always fire updateReader before it fires onAccessStarted/Ended
                // so this shouldn't happen anyway apart from a possible odd timing issue
            }

            // messages show above readers
            public override int GetItemViewType(int position)
                => position < m_messages.Count ? Resource.Layout.row_reader_message : Resource.Layout.row_reader;

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                View view = LayoutInflater.From(parent.Context).Inflate(viewType, parent, false);
                return (viewType == Resource.Layout.row_reader_message) ? (RecyclerView.ViewHolder)new MessageViewHolder(view) : new ReaderViewHolder(view);
            }

            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                if (holder is MessageViewHolder messageHolder)
                {
                    var message = m_messages[position];
                    messageHolder.ContentView.Text = message;
                }
                else if (holder is ReaderViewHolder readerHolder)
                {
                    var rws = m_readers[position - m_messages.Count];
                    var reader = rws.Reader;

                    var visualStateString = (rws.VisualState == ReaderVisualState.None ? null : rws.VisualState.ToString());
                    readerHolder.IdView.Text = visualStateString;
                    readerHolder.ContentView.Text = reader.Name;

                    readerHolder.Item = reader;
                    readerHolder.ItemView.Click += (sender, args) => m_parent.OnReaderClicked(reader);
                }
            }

            public override int ItemCount => m_messages.Count + m_readers.Count;

            // *********************************************************************************
            // ReaderUpdateListener
            // *********************************************************************************
            public void OnReaderUpdated(IReaderAttributes reader, ReaderUpdateType readerUpdateType)
            {
                if (readerUpdateType == ReaderUpdateType.AttributesChanged)
                {
                    int idx = FindReaderIndex(reader);
                    if (idx == -1)
                    { // a new reader, put it at the top
                        m_readers.Insert(0, new ReaderWithVisualState(reader, ReaderVisualState.None));
                        NotifyItemRangeInserted(0, 1);
                    }
                    else
                    {
                        m_readers[idx].Reader = reader;
                        NotifyItemRangeChanged(idx, 1);
                    }
                }
                else if (readerUpdateType == ReaderUpdateType.ReaderUnavailable)
                {
                    int idx = FindReaderIndex(reader);
                    if (idx >= 0)
                    { // only remove it if it's not already removed. We can get double-removes
                        m_readers.RemoveAt(idx);
                        NotifyItemRangeRemoved(idx, 1);
                    }
                }
            }

            class MessageViewHolder : RecyclerView.ViewHolder
            {
                public readonly TextView ContentView;

                public MessageViewHolder(View view) : base(view)
                    => ContentView = view.FindViewById<TextView>(Resource.Id.content);
            }

            class ReaderViewHolder : RecyclerView.ViewHolder
            {
                public readonly View View;
                public readonly TextView IdView;
                public readonly TextView ContentView;
                public IReader Item;

                public ReaderViewHolder(View view) : base(view)
                {
                    View = view;
                    IdView = view.FindViewById<TextView>(Resource.Id.id);
                    ContentView = view.FindViewById<TextView>(Resource.Id.content);
                }
            }
        }
    }
}