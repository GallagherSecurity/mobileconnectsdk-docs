package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.os.Bundle;
import com.google.android.material.bottomnavigation.BottomNavigationView;
import androidx.fragment.app.Fragment;
import androidx.fragment.app.FragmentPagerAdapter;
import androidx.viewpager.widget.ViewPager;
import androidx.appcompat.app.AppCompatActivity;

public class MainActivity extends AppCompatActivity  {

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
                return 2;
            }

            @Override
            public Fragment getItem(int position) {
                return position == 0 ? new RegistrationsFragment() :  new ReadersFragment();
            }
        };
        viewPager.setAdapter(mFragmentPagerAdapter);
        viewPager.addOnPageChangeListener(new ViewPager.SimpleOnPageChangeListener() {
            @Override
            public void onPageSelected(int position) {
                if(position == 0) {
                    setTitle("Credentials");
                    navigationView.setSelectedItemId(R.id.action_registration);
                } else {
                    setTitle("Access");
                    navigationView.setSelectedItemId(R.id.action_readers);
                }
            }
        });

        navigationView.setOnNavigationItemSelectedListener(item -> {
            int pageIndex = item.getItemId() == R.id.action_registration ? 0 : 1;

            viewPager.clearFocus();
            viewPager.setCurrentItem(pageIndex, true);
            viewPager.invalidate();
            return true;
        });
    }
}
