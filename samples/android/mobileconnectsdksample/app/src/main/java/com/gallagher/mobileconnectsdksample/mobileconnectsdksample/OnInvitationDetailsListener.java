//
// Copyright Gallagher Group Ltd 2024 All Rights Reserved
//
package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import androidx.annotation.Nullable;

public interface OnInvitationDetailsListener {
    public void onInvitationDetails(boolean succeeded, @Nullable String invitationCode, @Nullable String serverHost);
}
