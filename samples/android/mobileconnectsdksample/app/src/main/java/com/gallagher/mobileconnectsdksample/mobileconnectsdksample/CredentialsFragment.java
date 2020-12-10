package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.content.Context;
import android.os.Bundle;

import com.gallagher.security.mobileaccess.DeleteOption;
import com.google.android.material.floatingactionbutton.FloatingActionButton;

import androidx.appcompat.app.AlertDialog;
import androidx.recyclerview.widget.LinearLayoutManager;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.recyclerview.widget.RecyclerView;

import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;
import com.gallagher.security.mobileaccess.MobileCredential;
import com.gallagher.security.mobileaccess.RegistrationError;
import com.gallagher.security.mobileaccess.RegistrationListener;
import com.gallagher.security.mobileaccess.SecondFactorAuthenticationType;
import com.gallagher.security.mobileaccess.SecondFactorAuthenticationTypeSelector;

import java.net.URI;
import java.net.URISyntaxException;
import java.util.ArrayList;
import java.util.Collection;

public class CredentialsFragment extends Fragment implements RegisterMobileCredentialDialogFragment.OnInvitationDetailsListener, TabFragment {

    // *********************************************************************************
    // Get a reference to the MobileAccess shared instance
    // *********************************************************************************
    @NonNull
    private final MobileAccess mMobileAccess = MobileAccessProvider.getInstance();

    private MobileCredentialRecyclerViewAdapter mAdapter;

    public String getTitle() { return "Credentials"; }
    public int getActionId() { return R.id.action_credentials; }

    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        setRetainInstance(true);

        View view = inflater.inflate(R.layout.fragment_mobilecredential_list, container, false);

        Context context = view.getContext();

        // Wire up the RecyclerView
        mAdapter = new MobileCredentialRecyclerViewAdapter();
        // *********************************************************************************
        // Populate it with the current set of credentials
        // *********************************************************************************
        mAdapter.setCredentials(mMobileAccess.getMobileCredentials());

        RecyclerView recyclerView = view.findViewById(R.id.credential_list);
        recyclerView.setLayoutManager(new LinearLayoutManager(context));
        recyclerView.setAdapter(mAdapter);

        FloatingActionButton fab = view.findViewById(R.id.add_credential_fab);
        fab.setOnClickListener(v -> {
            // When someone clicks our FAB we popup a dialog asking for manual registration details
            RegisterMobileCredentialDialogFragment dlg = new RegisterMobileCredentialDialogFragment();
            dlg.setCancelable(false);
            dlg.setInvitationDetailsListener(this); // callback from the dialog
            //noinspection ConstantConditions
            dlg.show(getActivity().getFragmentManager(), "fragment_register_mobile_credential");
        });

        return view;
    }

    @Override
    public void onResume() {
        super.onResume();

        // In theory we only need to reload the credentials list after a successful add or remove of a credential.
        // It's easier just to blindly reload the list onResume and we only usually have one or maybe two
        // credentials so optimising isn't worth it.
        if (mAdapter != null) {
            mAdapter.setCredentials(mMobileAccess.getMobileCredentials());
        }
    }

    public void onMobileCredentialClicked(MobileCredential item) {
        //noinspection ConstantConditions
        new AlertDialog.Builder(getActivity())
                .setMessage("Are you sure you want to delete the credential for " + item.getFacilityName())
                .setPositiveButton("Yes", (dlg, which) -> {
                    // *********************************************************************************
                    // Ask the Mobile Connect SDK to delete our credential
                    // *********************************************************************************
                    mMobileAccess.deleteMobileCredential(item, DeleteOption.DEFAULT, (credential, error) -> {
                        if(error != null) {
                            Log.e("CredentialsFragment", "Error deleting credential", error);
                            Toast.makeText(getActivity(), "Error " + error.getLocalizedMessage(), Toast.LENGTH_LONG).show();
                        } else {
                            Toast.makeText(getActivity(), "Deleted!", Toast.LENGTH_SHORT).show();
                            mAdapter.setCredentials(mMobileAccess.getMobileCredentials());
                        }
                    });
                })
                .setNegativeButton("No", null)
                .show();
    }

    // When the manual registration details dialog closes, it will pass it's result here
    @Override
    public void onInvitationDetails(boolean succeeded, @Nullable String invitationCode, @Nullable String serverHost) {
        if (succeeded && serverHost != null && invitationCode != null) {
            try {
                // *********************************************************************************
                // When doing manual registration we build the URI ourselves.
                // Normally we'd expect the full URI to be passed to us e.g. from an email hyperlink,
                // or from some other custom code (perhaps you pass the URI through your own web-service
                // *********************************************************************************
                URI invitationUri = mMobileAccess.resolveInvitationUri(serverHost, invitationCode);

                // *********************************************************************************
                // Ask the Mobile Connect SDK to register our credential
                // *********************************************************************************
                mMobileAccess.registerCredential(invitationUri, new RegistrationListener() {

                    @Override
                    public void onRegistrationCompleted(@Nullable MobileCredential credential, @Nullable RegistrationError error) {
                        if(error != null) {
                            Log.e("CredentialsFragment", "Registration Error", error);
                            Toast.makeText(getActivity(), error.getLocalizedMessage(), Toast.LENGTH_LONG).show();
                        } else if(credential != null) {
                            Toast.makeText(getActivity(), "Registered!", Toast.LENGTH_SHORT).show();
                            mAdapter.setCredentials(mMobileAccess.getMobileCredentials()); // reload the list
                        }
                    }

                    // If the credential allows for second-factor authentication, then we need to ask
                    // the user which method they'd prefer; Fingerprint or PIN?
                    @Override
                    public void onAuthenticationTypeSelectionRequested(SecondFactorAuthenticationTypeSelector selector) {
                        //noinspection ConstantConditions
                        new AlertDialog.Builder(getActivity())
                                .setMessage("Please select second factor authentication type")
                                .setCancelable(false)
                                .setPositiveButton("Fingerprint", (dlg, which) -> selector.select(true, SecondFactorAuthenticationType.FINGERPRINT))
                                .setNegativeButton("Passcode", (dlg, which) -> selector.select(true, SecondFactorAuthenticationType.PIN))
                                .setNeutralButton("Cancel", (dlg, which) -> selector.select(false, null))
                                .show();
                    }
                });
            } catch (URISyntaxException e) {
                Log.e("CredentialsFragment", e.getLocalizedMessage());
            }
        }
    }

    class MobileCredentialRecyclerViewAdapter extends RecyclerView.Adapter<MobileCredentialRecyclerViewAdapter.ViewHolder> {

        // we take a copy of the credentials array to avoid possible bugs where the
        // recyclerview is in the middle of loading something while the SDK changes credentials
        // out from under it.
        @NonNull
        private final ArrayList<MobileCredential> mCredentials = new ArrayList<>();

        MobileCredentialRecyclerViewAdapter() { }

        void setCredentials(Collection<MobileCredential> credentials) {
            mCredentials.clear();
            mCredentials.addAll(credentials);
            notifyDataSetChanged();
        }

        @Override
        @NonNull
        public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
            View view = LayoutInflater.from(parent.getContext()).inflate(R.layout.row_mobilecredential, parent, false);
            return new ViewHolder(view);
        }

        @Override
        public void onBindViewHolder(@NonNull final ViewHolder holder, int position) {
            holder.mItem = mCredentials.get(position);
            holder.mIdView.setText(String.valueOf(mCredentials.get(position).getFacilityId()));
            holder.mContentView.setText(mCredentials.get(position).getFacilityName());

            holder.mView.setOnClickListener(v -> CredentialsFragment.this.onMobileCredentialClicked(holder.mItem));
        }

        @Override
        public int getItemCount() {
            return mCredentials.size();
        }

        class ViewHolder extends RecyclerView.ViewHolder {
            final View mView;
            final TextView mIdView;
            final TextView mContentView;
            MobileCredential mItem;

            ViewHolder(View view) {
                super(view);
                mView = view;
                mIdView = view.findViewById(R.id.id);
                mContentView = view.findViewById(R.id.content);
            }
        }
    }
}
