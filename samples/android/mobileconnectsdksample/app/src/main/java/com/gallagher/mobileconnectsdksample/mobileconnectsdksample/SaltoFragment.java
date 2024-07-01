//
// Copyright Gallagher Group Ltd 2024 All Rights Reserved
//
package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;
import com.gallagher.security.mobileaccess.SaltoAccessListener;
import com.gallagher.security.mobileaccess.SaltoAccessResult;
import com.gallagher.security.mobileaccess.SaltoError;
import com.gallagher.security.mobileaccess.SaltoKeyIdentifier;
import com.gallagher.security.mobileaccess.SaltoUpdateListener;
import com.gallagher.security.mobileaccess.SdkFeatureState;
import com.gallagher.security.mobileaccess.SdkFeatureStateListener;
import com.gallagher.security.mobileaccess.SaltoOpeningMode;
import com.gallagher.security.mobileaccess.SaltoOpeningParams;

import java.util.ArrayList;
import java.util.Collection;
import java.util.List;

public class SaltoFragment extends Fragment implements SdkFeatureStateListener, TabFragment {

    @NonNull
    private final MobileAccess mMobileAccess = MobileAccessProvider.getInstance();

    @NonNull
    private final SaltoRecyclerViewAdapter mAdapter = new SaltoRecyclerViewAdapter();

    private View mBannerView;
    private boolean mHasCloudConnectionError = false;

    public String getTitle() { return "Salto Access"; }
    public int getActionId() { return R.id.action_salto_keys; }

    public SaltoFragment() { }

    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        View view = inflater.inflate(R.layout.fragment_salto_list, container, false);

        RecyclerView recyclerView = view.findViewById(R.id.salto_list);
        recyclerView.setLayoutManager(new LinearLayoutManager(view.getContext()));
        recyclerView.setAdapter(mAdapter);

        mBannerView = view.findViewById(R.id.banner);
        Button bannerRetryButton = view.findViewById(R.id.bannerRetryButton);
        bannerRetryButton.setOnClickListener(v -> mMobileAccess.syncCredentialItemUpdates());

        mMobileAccess.addSdkFeatureStateListener(this);
        mMobileAccess.addSaltoUpdateListener(mAdapter);

        return view;
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        mMobileAccess.removeSdkFeatureStateListener(this);
        mMobileAccess.removeSaltoUpdateListener(mAdapter);
    }

    @Override
    public void onResume() {
        super.onResume();
        mMobileAccess.syncCredentialItemUpdates();
        updateConnectionErrorBanner();
    }

    @Override
    public void onFeatureStatesChanged(@NonNull Collection<SdkFeatureState> featureStates) {
        mHasCloudConnectionError = featureStates.contains(SdkFeatureState.ERROR_CLOUD_CONNECTION_FAILED);
        updateConnectionErrorBanner();
    }

    @Override
    public void onFeatureError(@NonNull Error error) {
        if (error instanceof SaltoError) {
            Toast.makeText(getContext(), String.format("Salto key update error%n %s", error.getLocalizedMessage()), Toast.LENGTH_LONG).show();
        }
    }

    public void onSaltoUnlockStandardModeButtonClicked(SaltoKeyIdentifier saltoKey)
    {
        Toast.makeText(getContext(), String.format("Unlock using key %s in standard mode", saltoKey.getName()), Toast.LENGTH_SHORT).show();

        // if no SaltoOpeningParams passed then SaltoOpeningMode.STANDARD_MODE is used by default
        mMobileAccess.startOpeningSaltoDoor(saltoKey, new SaltoAccessListener() {
            @Override
            public void onPeripheralFound() {
                Toast.makeText(getContext(), "Salto door detected", Toast.LENGTH_SHORT).show();
            }

            @Override
            public void onSaltoAccessCompleted(@Nullable SaltoAccessResult saltoAccessResult, @Nullable SaltoError error) {
                if (error != null) {
                    Toast.makeText(getContext(), String.format("Salto access failed%n %s", error.getLocalizedMessage()), Toast.LENGTH_LONG).show();
                }
                else if (saltoAccessResult != null) {
                    Toast.makeText(getContext(), String.format("Salto access result%n %s", saltoAccessResult), Toast.LENGTH_LONG).show();
                }
                else {
                    throw new IllegalStateException("Missing result or error from salto access complete callback");
                }
            }
        });
    }

    public void onSaltoUnlockOfficeModeButtonClicked(SaltoKeyIdentifier saltoKey)
    {
        Toast.makeText(getContext(), String.format("Unlock using key %s in office mode", saltoKey.getName()), Toast.LENGTH_SHORT).show();

        mMobileAccess.startOpeningSaltoDoor(saltoKey, new SaltoAccessListener() {
            @Override
            public void onPeripheralFound() {
                Toast.makeText(getContext(), "Salto door detected", Toast.LENGTH_SHORT).show();
            }

            @Override
            public void onSaltoAccessCompleted(@Nullable SaltoAccessResult saltoAccessResult, @Nullable SaltoError error) {
                if (error != null) {
                    Toast.makeText(getContext(), String.format("Salto access failed%n %s", error.getLocalizedMessage()), Toast.LENGTH_LONG).show();
                }
                else if (saltoAccessResult != null) {
                    Toast.makeText(getContext(), String.format("Salto access result%n %s", saltoAccessResult), Toast.LENGTH_LONG).show();
                }
                else {
                    throw new IllegalStateException("Missing result or error from salto access complete callback");
                }
            }
        }, new SaltoOpeningParams(SaltoOpeningMode.OFFICE_MODE));
    }

    private void updateConnectionErrorBanner() {
        if (mBannerView != null)
            mBannerView.setVisibility(mHasCloudConnectionError ? View.VISIBLE : View.GONE);
    }

    class SaltoViewHolder extends RecyclerView.ViewHolder {
        private final TextView mIdView;
        private final TextView mContentView;
        private SaltoKeyIdentifier mSaltoKey;

        SaltoViewHolder(View view) {
            super(view);
            mIdView = view.findViewById(R.id.id);
            mContentView = view.findViewById(R.id.content);
        }

        public void setSaltoKey(@NonNull SaltoKeyIdentifier key) {
            mSaltoKey = key;
            mIdView.setText(key.getName());
            mContentView.setText(key.getSaltoServerId().toString());
        }

        public @NonNull SaltoKeyIdentifier getSaltoKey() { return mSaltoKey; }
    }

    public class SaltoRecyclerViewAdapter extends RecyclerView.Adapter<SaltoViewHolder> implements SaltoUpdateListener {

        @NonNull
        private final ArrayList<SaltoKeyIdentifier> mSaltoKeys = new ArrayList<>();

        @NonNull
        @Override
        public SaltoViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
            View view = LayoutInflater.from(parent.getContext()).inflate(R.layout.row_salto_key, parent, false);
            return new SaltoViewHolder(view);
        }

        @Override
        public void onBindViewHolder(@NonNull SaltoViewHolder holder, int position) {
            holder.setSaltoKey(mSaltoKeys.get(position));
            Button unlockStandardModeButton = holder.itemView.findViewById(R.id.unlock_standard_mode_button);
            unlockStandardModeButton.setOnClickListener(v ->
                    onSaltoUnlockStandardModeButtonClicked(holder.getSaltoKey()));
            Button unlockOfficeModeButton = holder.itemView.findViewById(R.id.unlock_office_mode_button);
            unlockOfficeModeButton.setOnClickListener(v ->
                    onSaltoUnlockOfficeModeButtonClicked(holder.getSaltoKey()));

        }

        @Override
        public int getItemCount() {
            return mSaltoKeys.size();
        }

        @Override
        public void onSaltoKeysUpdated(@NonNull List<SaltoKeyIdentifier> addedOrUpdatedSaltoKeyIdentifiers, @NonNull List<SaltoKeyIdentifier> removedSaltoKeyIdentifiers) {
            // update our list of Salto Key Identifiers
            for (SaltoKeyIdentifier saltoKey : addedOrUpdatedSaltoKeyIdentifiers) {
                int updateIndex = -1;
                for (int i = 0; i < mSaltoKeys.size(); i++) {
                    SaltoKeyIdentifier key = mSaltoKeys.get(i);
                    if (key.getCredentialId().equals(saltoKey.getCredentialId()) && key.getSaltoServerId().equals(saltoKey.getSaltoServerId())) {
                        updateIndex = i;
                        break;
                    }
                }

                if (updateIndex == -1) {
                    mSaltoKeys.add(saltoKey);
                    notifyItemInserted(mSaltoKeys.size() - 1);
                } else {
                    mSaltoKeys.set(updateIndex, saltoKey);
                    notifyItemChanged(updateIndex);
                }
            }

            for (SaltoKeyIdentifier removedSaltoKey : removedSaltoKeyIdentifiers) {
                for (int i = 0; i < mSaltoKeys.size(); i++) {
                    SaltoKeyIdentifier key = mSaltoKeys.get(i);
                    if (key.getCredentialId().equals(removedSaltoKey.getCredentialId()) && key.getSaltoServerId().equals(removedSaltoKey.getSaltoServerId())) {
                        mSaltoKeys.remove(i);
                        notifyItemRemoved(i);
                        break;
                    }
                }
            }
        }
    }
}
