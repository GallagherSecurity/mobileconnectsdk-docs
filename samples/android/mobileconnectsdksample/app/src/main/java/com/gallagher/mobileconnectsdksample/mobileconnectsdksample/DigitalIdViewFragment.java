package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.DialogFragment;

import com.gallagher.security.mobileaccess.DigitalId;

public class DigitalIdViewFragment extends DialogFragment {

    private DigitalId mDigitalId;
    private ImageView mImageView;

    public DigitalIdViewFragment() { }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        View view = inflater.inflate(R.layout.fragment_digital_id_view, container, false);

        mImageView = view.findViewById(R.id.imageView);

        byte[] imageBytes = mDigitalId.getFrontSide();
        Bitmap image = BitmapFactory.decodeByteArray(imageBytes, 0, imageBytes.length);
        mImageView.setImageBitmap(image);

        return view;
    }

    public void setDigitalId(DigitalId digitalId) {
        mDigitalId = digitalId;
    }
}
