//
// Copyright Gallagher Group Ltd 2024 All Rights Reserved
//
package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.view.Menu;
import android.view.MenuItem;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.appcompat.app.ActionBarDrawerToggle;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.widget.Toolbar;
import androidx.core.content.FileProvider;
import androidx.drawerlayout.widget.DrawerLayout;
import androidx.fragment.app.Fragment;
import androidx.fragment.app.FragmentPagerAdapter;
import androidx.viewpager.widget.ViewPager;

import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;
import com.google.android.material.bottomnavigation.BottomNavigationView;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.File;
import java.util.ArrayList;

public class MainActivity extends AppCompatActivity {

    Logger LOG = LoggerFactory.getLogger(MainActivity.class);

    @NonNull
    private final MobileAccess mMobileAccess = MobileAccessProvider.getInstance();

    @NonNull
    private final TabFragment[] mTabs = {
            new CredentialsFragment(),
            new ReadersFragment(),
            new SaltoFragment(),
            new DigitalIdFragment()
    };

    FragmentPagerAdapter mFragmentPagerAdapter;

    private ActionBarDrawerToggle mToggle;

    private void emailLogFiles() {
        ArrayList<Uri> uris = new ArrayList<>();
        File logDir;
        try {
            logDir = Application.getLogFilesDir(this);
            if (logDir == null) {
                LOG.error("Can't email log files; Application.getLogFileDir returned null");
                return;
            }
            File[] files = logDir.listFiles();
            if (files != null) {
                for (File log : files) {
                    Uri fileUri = FileProvider.getUriForFile(this, "com.gallagher.mobileconnectsdksample.FileProvider", log);
                    boolean success = uris.add(fileUri);
                    LOG.info("log file: '{}', added: '{}'", log.getName(), success);
                }
            }
        } catch (Exception e) {
            LOG.error("Failed to get log files due to: ", e);
            return;
        }

        Intent email = new Intent(Intent.ACTION_SEND_MULTIPLE);
        email.setType("message/rfc822");
        email.putExtra(Intent.EXTRA_SUBJECT, "Gallagher Sample App Logs");
        email.putParcelableArrayListExtra(Intent.EXTRA_STREAM, uris);
        email.setFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);

        if (email.resolveActivity(getApplicationContext().getPackageManager()) != null) {
            startActivity(email);
        } else {
            LOG.debug("Email is not available on this device");
        }
    }


    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        setTitle("Credentials"); // we start on the credentials page

        ViewPager viewPager = findViewById(R.id.pager);
        BottomNavigationView navigationView = findViewById(R.id.navigation_view);

        // setup the toolbar/actionbar
        Toolbar toolbar = findViewById(R.id.toolbar);
        setSupportActionBar(toolbar);

        DrawerLayout drawerLayout = findViewById(R.id.drawer_layout);
        mToggle = new ActionBarDrawerToggle(this, drawerLayout, R.string.settings_menu_open, R.string.settings_menu_close);
        drawerLayout.addDrawerListener(mToggle);
        mToggle.syncState();

        toolbar.setOnMenuItemClickListener(item -> {
            if (item.getItemId() == R.id.send_logs) {
                emailLogFiles();
                return true;
            }
            return true;

        });


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
    public void setTitle(CharSequence title) {
        TextView toolbarTitle = findViewById(R.id.toolbar_title);
        toolbarTitle.setText(title);
    }

    @Override
    protected void onResume() {
        super.onResume();
        mMobileAccess.syncCredentialItemUpdates();
    }

    @Override
    public boolean onCreateOptionsMenu(Menu menu) {
        getMenuInflater().inflate(R.menu.settings_menu, menu);
        return true;
    }

    @Override
    public boolean onOptionsItemSelected(@NonNull MenuItem item) {
        if (mToggle.onOptionsItemSelected(item)) {
            return true;
        }
        return super.onOptionsItemSelected(item);
    }
}
