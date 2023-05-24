//
// Copyright Gallagher Group Ltd 2022 All Rights Reserved
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

import com.gallagher.security.mobileaccess.DigitalId;
import com.gallagher.security.mobileaccess.DigitalIdError;
import com.gallagher.security.mobileaccess.DigitalIdListener;
import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;
import com.gallagher.security.mobileaccess.SdkFeatureState;
import com.gallagher.security.mobileaccess.SdkFeatureStateListener;

import java.util.ArrayList;
import java.util.Collection;
import java.util.Date;
import java.util.List;

public class DigitalIdFragment extends Fragment implements TabFragment, SdkFeatureStateListener {

    @NonNull
    private final MobileAccess mMobileAccess = MobileAccessProvider.getInstance();

    @NonNull
    private final DigitalIdRecyclerViewAdapter mAdapter = new DigitalIdRecyclerViewAdapter();

    @NonNull
    private DigitalIdViewFragment mViewFragment = new DigitalIdViewFragment();

    private View mBannerView;
    private boolean mHasCloudConnectionError = false;

    public String getTitle() { return "Digital IDs"; }
    public int getActionId() { return R.id.action_digital_ids; }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        View view = inflater.inflate(R.layout.fragment_digital_id_list, container, false);

        RecyclerView recyclerView = view.findViewById(R.id.digital_id_list);
        recyclerView.setLayoutManager(new LinearLayoutManager(view.getContext()));
        recyclerView.setAdapter(mAdapter);

        mBannerView = view.findViewById(R.id.banner);
        Button bannerRetryButton = view.findViewById(R.id.bannerRetryButton);
        bannerRetryButton.setOnClickListener(v -> mMobileAccess.syncCredentialItemUpdates());

        mMobileAccess.addSdkFeatureStateListener(this);
        mMobileAccess.addDigitalIdListener(mAdapter);

        return view;
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        mMobileAccess.removeSdkFeatureStateListener(this);
        mMobileAccess.removeDigitalIdListener(mAdapter);
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
        if (error instanceof DigitalIdError) {
            Toast.makeText(getContext(), String.format("Digital Id update error%n %s", error.getLocalizedMessage()), Toast.LENGTH_LONG).show();
        }
    }

    public void onDigitalIdClicked(DigitalId digitalId) {
        mViewFragment.setDigitalId(digitalId);
        mViewFragment.show(getActivity().getSupportFragmentManager(), null);
    }

    private void updateConnectionErrorBanner() {
        if (mBannerView != null)
            mBannerView.setVisibility(mHasCloudConnectionError ? View.VISIBLE : View.GONE);
    }

    class DigitalIdViewHolder extends RecyclerView.ViewHolder {
        private final TextView mIdView;
        private final TextView mContentView;
        private DigitalId mDigitalId;

        DigitalIdViewHolder(View view) {
            super(view);
            mIdView = view.findViewById(R.id.id);
            mContentView = view.findViewById(R.id.content);
        }

        public void setDigitalId(@NonNull DigitalId digitalId) {
            mDigitalId = digitalId;
            mIdView.setText(digitalId.getName());
            mContentView.setText(digitalId.getStatusValue());
        }

        @NonNull
        public DigitalId getDigitalId() { return mDigitalId; }
    }

    class DigitalIdRecyclerViewAdapter extends RecyclerView.Adapter<DigitalIdViewHolder> implements DigitalIdListener {

        @NonNull
        private ArrayList<DigitalId> mDigitalIds = new ArrayList<>();

        @NonNull
        @Override
        public DigitalIdViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
            View view = LayoutInflater.from(parent.getContext()).inflate(R.layout.row_digital_id, parent, false);
            return new DigitalIdViewHolder(view);
        }

        @Override
        public void onBindViewHolder(@NonNull DigitalIdViewHolder holder, int position) {
            holder.setDigitalId(mDigitalIds.get(position));
            holder.itemView.setOnClickListener(v ->
                    DigitalIdFragment.this.onDigitalIdClicked(holder.getDigitalId()));
        }

        @Override
        public int getItemCount() {
            return mDigitalIds.size();
        }

        @Override
        public void onDigitalIdUpdated(@NonNull List<DigitalId> addedOrUpdatedDigitalIds, @NonNull List<DigitalId> removedDigitalIds, @Nullable Date lastUpdateTime) {
            // update our list of DigitalIds
            for (DigitalId digitalId : addedOrUpdatedDigitalIds) {
                int updateIndex = -1;
                for (int i = 0; i < mDigitalIds.size(); i++) {
                    DigitalId id = mDigitalIds.get(i);
                    if (id.getId().equals(digitalId.getId())) {
                        updateIndex = i;
                        break;
                    }
                }

                if (updateIndex == -1) {
                    mDigitalIds.add(digitalId);
                    notifyItemInserted(mDigitalIds.size() - 1);
                } else {
                    mDigitalIds.set(updateIndex, digitalId);
                    notifyItemChanged(updateIndex);
                }
            }

            for (DigitalId removedDigitalId : removedDigitalIds) {
                for (int i = 0; i < mDigitalIds.size(); i++) {
                    DigitalId digitalId = mDigitalIds.get(i);
                    if (digitalId.getId().equals(removedDigitalId.getId())) {
                        mDigitalIds.remove(i);
                        notifyItemRemoved(i);
                        break;
                    }
                }
            }
        }
    }
}
