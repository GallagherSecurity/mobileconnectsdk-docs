//
// Copyright Gallagher Group Ltd 2024 All Rights Reserved
//
package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.os.Bundle;

import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.fragment.app.Fragment;
import androidx.fragment.app.FragmentPagerAdapter;
import androidx.viewpager.widget.ViewPager;

import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;
import com.google.android.material.bottomnavigation.BottomNavigationView;

public class MainActivity extends AppCompatActivity {

    @NonNull
    private final MobileAccess mMobileAccess = MobileAccessProvider.getInstance();

    @NonNull
    private final TabFragment[] mTabs = {
            new CredentialsFragment(),
            new ReadersFragment(),
            new SaltoFragment(),
            new DigitalIdFragment(),
    };

    FragmentPagerAdapter mFragmentPagerAdapter;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        setTitle("Credentials"); // we start on the credentials page

        ViewPager viewPager = findViewById(R.id.pager);
        BottomNavigationView navigationView = findViewById(R.id.navigation_view);

        mFragmentPagerAdapter = new FragmentPagerAdapter(getSupportFragmentManager()) {
            @Override
            public int getCount() {
                return mTabs.length;
            }

            @Override
            public Fragment getItem(int position) {
                return (Fragment)mTabs[position];
            }
        };
        viewPager.setAdapter(mFragmentPagerAdapter);
        viewPager.addOnPageChangeListener(new ViewPager.SimpleOnPageChangeListener() {
            @Override
            public void onPageSelected(int position) {
                TabFragment tab = mTabs[position];
                setTitle(tab.getTitle());
                navigationView.setSelectedItemId(tab.getActionId());
            }
        });

        navigationView.setOnNavigationItemSelectedListener(item -> {
            int pageIndex = -1;
            for (int i = 0; i < mTabs.length; i++) {
                if (mTabs[i].getActionId() == item.getItemId()) {
                    pageIndex = i;
                    break;
                }
            }

            if (pageIndex == -1)
                throw new IllegalStateException("Missing tab fragment!");

            viewPager.clearFocus();
            viewPager.setCurrentItem(pageIndex, true);
            viewPager.invalidate();
            return true;
        });
    }

    @Override
    protected void onResume() {
        super.onResume();
        mMobileAccess.syncCredentialItemUpdates();
    }
}
