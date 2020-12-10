package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.Manifest;
import android.content.Context;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import androidx.recyclerview.widget.LinearLayoutManager;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.recyclerview.widget.RecyclerView;

import com.gallagher.security.mobileaccess.AccessResult;
import com.gallagher.security.mobileaccess.AutomaticAccessListener;
import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;
import com.gallagher.security.mobileaccess.MobileAccessState;
import com.gallagher.security.mobileaccess.Reader;
import com.gallagher.security.mobileaccess.ReaderAttributes;
import com.gallagher.security.mobileaccess.ReaderConnectionError;
import com.gallagher.security.mobileaccess.ReaderUpdateListener;
import com.gallagher.security.mobileaccess.ReaderUpdateType;
import com.gallagher.security.mobileaccess.SdkStateListener;

import java.util.ArrayList;
import java.util.Collection;
import java.util.List;

public class ReadersFragment extends Fragment implements SdkStateListener, AutomaticAccessListener, TabFragment {

    static final int PERMISSION_REQUEST_FINE_LOCATION = 2;

    private enum ReaderVisualState {
        CONNECTING, GRANTED, DENIED
    }

    // "ViewModel" to render our reader information along with connection state
    private static class ReaderWithVisualState {
        ReaderAttributes Reader;
        ReaderVisualState VisualState;

        ReaderWithVisualState(ReaderAttributes reader, ReaderVisualState visualState) {
            Reader = reader;
            VisualState = visualState;
        }
    }

    // *********************************************************************************
    // Get a reference to the MobileAccess shared instance
    // *********************************************************************************
    @NonNull
    private final MobileAccess mMobileAccess = MobileAccessProvider.getInstance();

    ReaderRecyclerViewAdapter mAdapter;

    public String getTitle() { return "Readers"; }
    public int getActionId() { return R.id.action_readers; }

    public ReadersFragment() { }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        View view = inflater.inflate(R.layout.fragment_reader_list, container, false);

        Context context = view.getContext();

        // Wire up the RecyclerView
        mAdapter = new ReaderRecyclerViewAdapter();

        // *********************************************************************************
        // Ask the SDK to tell us about it's operational state so we can show warning messages if needed
        // *********************************************************************************
        mMobileAccess.addSdkStateListener(this);

        // *********************************************************************************
        // Ask the SDK to tell us about readers it discovers
        // *********************************************************************************
        mMobileAccess.addReaderUpdateListener(mAdapter);

        // *********************************************************************************
        // Ask the SDK to tell us about automatic access so we can show UI if needed
        // *********************************************************************************
        mMobileAccess.addAutomaticAccessListener(this);

        RecyclerView recyclerView = view.findViewById(R.id.reader_list);
        recyclerView.setLayoutManager(new LinearLayoutManager(context));
        recyclerView.setAdapter(mAdapter);

        return view;
    }

    @Override
    public void onDestroyView() {
        mMobileAccess.removeAutomaticAccessListener(this);
        mMobileAccess.removeReaderUpdateListener(mAdapter);
        mMobileAccess.removeSdkStateListener(this);
        super.onDestroyView();
    }

    // *********************************************************************************
    // Manually request access for the given reader
    // *********************************************************************************
    public void onReaderClicked(Reader reader) {
        mMobileAccess.requestAccess(reader, this);
    }

    // *********************************************************************************
    // SdkStateListener:
    // The MobileConnect SDK will publish the list of problems/warnings via this callback.
    // so we can use it to show warning messages and things like that
    // *********************************************************************************
    public void onStateChanged(boolean isScanning, Collection<MobileAccessState> states) {
        ArrayList<String> messages = new ArrayList<>();

        for(MobileAccessState state : states) {
            switch (state) {
                case ERROR_NO_CREDENTIALS:
                    messages.add("Please register a credential");
                    break;
                case NO_NFC_FEATURE:
                    // A reasonable subset of Android phones do not support NFC, so it is likely
                    // you don't want to warn about lack of NFC; They can just use Bluetooth instead
                    messages.add("This device does not support NFC");
                    break;

                case BLE_ERROR_DISABLED:
                    messages.add("Bluetooth is disabled; Please enable it to allow access using Bluetooth");
                    break;
                case NFC_ERROR_DISABLED:
                    messages.add("NFC is disabled; Please enable it to allow access using NFC");
                    break;

                case BLE_ERROR_NO_LOCATION_PERMISSION:
                    messages.add("Please grant permission for this application to use your location.");
                    // Request location permissions from user.
                    // It's recommended you do this in a more sensible place so as not to spam the user with requests
                    if (android.os.Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                        requestPermissions(new String[]{Manifest.permission.ACCESS_FINE_LOCATION}, PERMISSION_REQUEST_FINE_LOCATION);
                    }
                    break;
                case BLE_ERROR_LOCATION_SERVICE_DISABLED:
                    messages.add("Location services are disabled; Please enable them to allow bluetooth connectivity.");
                    break;

                case ERROR_NO_BLE_FEATURE:
                    // This isn't likely to happen
                    // Even ultra cheap $20 android devices released 5 years ago tend to  have bluetooth
                    messages.add("This device does not support bluetooth");
                    break;
                case ERROR_UNSUPPORTED_OS_VERSION:
                    // The Mobile Connect SDK requires android version 5 or newer
                    // You can use the google play store to limit installation
                    // of your app to android 5+ to avoid this ever happening
                    messages.add("Your device operating system version is not supported");
                    break;

                case BLE_ERROR_NO_BACKGROUND_LOCATION_PERMISSION:
                    // Android 10+ requires the new ACCESS_BACKGROUND_LOCATION permission in order to use BLE in the background.
                    // If you have configured BLE background access, then you must enable this permission for it to work properly
                    messages.add("Please grant permission for this application to Always use your location. This is required for BLE to work in the background.");
                    break;
            }
        }
        mAdapter.setMessages(messages);
    }


    // *********************************************************************************
    // AutomaticAccessListener:
    // The MobileConnect SDK is telling us we need to put the phone back next to the reader
    // in order to complete an NFC transaction.
    // *********************************************************************************
    @Override
    public void onReturnToReaderRequired(Reader reader) {
        // This is handled at the Application level so we can ignore it
    }

    // *********************************************************************************
    // AutomaticAccessListener:
    // *********************************************************************************
    @Override
    public void onReturnedToReader(Reader reader) {
        // This is handled at the Application level so we can ignore it
    }

    // *********************************************************************************
    // AutomaticAccessListener (which is also the AccessListener for manual connect requests):
    // The MobileConnect SDK is telling us access is in progress for the given reader
    // so we may update the appropriate UI for that reader (e.g. show an animation)
    // *********************************************************************************
    @Override
    public void onAccessStarted(@NonNull Reader reader) {
        mAdapter.setReaderVisualState(reader, ReaderVisualState.CONNECTING);
    }

    // *********************************************************************************
    // AutomaticAccessListener:
    // The MobileConnect SDK is telling us access completed for the given reader
    // *********************************************************************************
    @Override
    public void onAccessCompleted(@NonNull Reader reader, @Nullable AccessResult accessResult, @Nullable ReaderConnectionError error) {
        // 'error' only occurs if there's some sort of lower-level error (e.g. bluetooth disconnect)
        // in the normal case error will be null, and you should check accessResult.isAccessGranted().
        // accessResult.getAccessDecision() is the actual specific result behind the scenes
        if(accessResult != null && accessResult.isAccessGranted()) {
            mAdapter.setReaderVisualState(reader, ReaderVisualState.GRANTED);
        } else {
            mAdapter.setReaderVisualState(reader, ReaderVisualState.DENIED);
        }

        // The SDK doesn't give us any more callbacks after accessComplete
        // so set a timer to clear the visual state after a small delay.
        new Handler(Looper.getMainLooper()).postDelayed(() -> mAdapter.setReaderVisualState(reader, null), 1000);
    }

    class ReaderRecyclerViewAdapter extends RecyclerView.Adapter implements ReaderUpdateListener {

        @NonNull private final ArrayList<String> mMessages = new ArrayList<>();
        @NonNull private final ArrayList<ReaderWithVisualState> mReaders = new ArrayList<>();

        ReaderRecyclerViewAdapter() { }

        void setMessages(List<String> messages) {
            mMessages.clear();
            mMessages.addAll(messages);
            notifyDataSetChanged();
        }

        // *********************************************************************************
        // Updates to a reader will have different attributes and therefore
        // it doesn't really make sense to compare them directly. Instead we search for
        // "matching" readers with the same ID
        // *********************************************************************************
        int findReaderIndex(@NonNull Reader reader) {
            for(int i = 0; i < mReaders.size(); i++) {
                if(mReaders.get(i).Reader.getId().equals(reader.getId())) {
                    return i;
                }
            }
            return -1;
        }

        void setReaderVisualState(Reader reader, ReaderVisualState visualState) {
            int idx = findReaderIndex(reader);
            if(idx >= 0) {
                mReaders.get(idx).VisualState = visualState;
                notifyItemRangeChanged(idx, 1);
            }
            // if we don't find the reader, ignore it.
            // The SDK will always fire updateReader before it fires onAccessStarted/Ended
            // so this shouldn't happen anyway apart from a possible odd timing issue
        }

        @Override
        public int getItemViewType(int position) {
            // messages show above readers
            return position < mMessages.size() ? R.layout.row_reader_message : R.layout.row_reader;
        }

        @Override
        @NonNull
        public RecyclerView.ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
            View view = LayoutInflater.from(parent.getContext()).inflate(viewType, parent, false);
            return viewType == R.layout.row_reader_message ? new MessageViewHolder(view) : new ReaderViewHolder(view);
        }

        @Override
        public void onBindViewHolder(@NonNull final RecyclerView.ViewHolder holder, int position) {
            if(holder instanceof MessageViewHolder) {
                MessageViewHolder messageHolder = (MessageViewHolder)holder;

                String message = mMessages.get(position);
                messageHolder.mContentView.setText(message);
            }
            else if(holder instanceof ReaderViewHolder) {
                ReaderViewHolder readerHolder = (ReaderViewHolder)holder;

                ReaderWithVisualState rws = mReaders.get(position - mMessages.size());
                ReaderAttributes reader = rws.Reader;

                String visualStateString = rws.VisualState != null ? rws.VisualState.toString() : null;
                readerHolder.mIdView.setText(visualStateString);
                readerHolder.mContentView.setText(reader.getName());

                readerHolder.mItem = reader;
                readerHolder.itemView.setOnClickListener(v -> ReadersFragment.this.onReaderClicked(readerHolder.mItem));
            }
        }

        @Override
        public int getItemCount() {
            // messages show above readers
            return mMessages.size() + mReaders.size();
        }

        // *********************************************************************************
        // ReaderUpdateListener
        // *********************************************************************************
        @Override
        public void onReaderUpdated(ReaderAttributes reader, ReaderUpdateType readerUpdateType) {
            if(readerUpdateType.equals(ReaderUpdateType.ATTRIBUTES_CHANGED)) {
                int idx = findReaderIndex(reader);
                if(idx == -1) { // a new reader, put it at the top
                    mReaders.add(0, new ReaderWithVisualState(reader, null));
                    notifyItemRangeInserted(0, 1);
                } else {
                    mReaders.get(idx).Reader = reader;
                    notifyItemRangeChanged(idx, 1);
                }
            } else if(readerUpdateType.equals(ReaderUpdateType.READER_UNAVAILABLE)) {
                int idx = findReaderIndex(reader);
                if(idx >= 0) { // only remove it if it's not already removed. We can get double-removes
                    mReaders.remove(idx);
                    notifyItemRangeRemoved(idx, 1);
                }
            }
        }

        class MessageViewHolder extends RecyclerView.ViewHolder {
            final TextView mContentView;

            MessageViewHolder(View view) {
                super(view);
                mContentView = view.findViewById(R.id.content);
            }
        }

        class ReaderViewHolder extends RecyclerView.ViewHolder {
            final View mView;
            final TextView mIdView;
            final TextView mContentView;
            Reader mItem;

            ReaderViewHolder(View view) {
                super(view);
                mView = view;
                mIdView = view.findViewById(R.id.id);
                mContentView = view.findViewById(R.id.content);
            }
        }
    }


}