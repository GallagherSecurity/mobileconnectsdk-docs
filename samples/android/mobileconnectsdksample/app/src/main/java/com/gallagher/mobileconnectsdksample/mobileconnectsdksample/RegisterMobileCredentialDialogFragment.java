package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.app.DialogFragment;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;

import androidx.annotation.Nullable;

import java.util.regex.Pattern;

public class RegisterMobileCredentialDialogFragment extends DialogFragment {

    public interface OnInvitationDetailsListener {
        void onInvitationDetails(boolean succeeded, @Nullable String invitationCode, @Nullable String serverHost);
    }

    @Nullable OnInvitationDetailsListener mInvitationDetailsListener;

    @Nullable
    @Override
    public View onCreateView(LayoutInflater inflater, @Nullable ViewGroup container, Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_register_mobile_credential, container);
    }

    @Override
    public void onViewCreated(View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        Button okButton = view.findViewById(R.id.ok_button);
        EditText invitationCodeText = view.findViewById(R.id.invitation_code);
        EditText serverHostText = view.findViewById(R.id.server_url);

        // Convenience for manual registration - if the clipboard looks like it has an invitation code in it, paste that in
        ClipboardManager clipboard = (ClipboardManager)getActivity().getSystemService(Context.CLIPBOARD_SERVICE);
        if(clipboard != null) {
            ClipData clip = clipboard.getPrimaryClip();
            if(clip != null && clip.getItemCount() > 0) {
                CharSequence clipText = clip.getItemAt(0).coerceToText(getActivity());
                if(clipText != null && Pattern.matches("\\w{4}-\\w{4}-\\w{4}-\\w{4}", clipText)) { // looks like an invitation code
                    invitationCodeText.setText(clipText);
                }
            }
        }

        okButton.setOnClickListener(v -> {
            if(mInvitationDetailsListener != null) {
                mInvitationDetailsListener.onInvitationDetails(true, invitationCodeText.getText().toString(), serverHostText.getText().toString());
            }
            dismiss();
        });

        Button cancelButton = view.findViewById(R.id.cancel_button);
        cancelButton.setOnClickListener(v -> {
            if(mInvitationDetailsListener != null) {
                mInvitationDetailsListener.onInvitationDetails(false, null, null);
            }
            dismiss();
        });
    }

    public void setInvitationDetailsListener(@Nullable OnInvitationDetailsListener listener) {
        mInvitationDetailsListener = listener;
    }
}
