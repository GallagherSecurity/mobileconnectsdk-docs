using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using System;
using System.Threading;

namespace mobileconnectsdksample_xamarin
{
    [Android.App.Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        class MyFragmentPagerAdapter : FragmentPagerAdapter
        {
            public MyFragmentPagerAdapter(FragmentManager fragmentManager) : base(fragmentManager)
            { }

            public override int Count => 2;

            public override Fragment GetItem(int position)
            {
                return position == 0 ? (Fragment)new RegistrationsFragment() : new ReadersFragment();
            }
        }
        
        FragmentPagerAdapter m_fragmentPagerAdapter;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Title = "Credentials"; // we start on the credentials page

            ViewPager viewPager = (ViewPager)FindViewById(Resource.Id.pager);
            BottomNavigationView navigationView = (BottomNavigationView)FindViewById(Resource.Id.navigation_view);

            m_fragmentPagerAdapter = new MyFragmentPagerAdapter(SupportFragmentManager);
        
            viewPager.Adapter = m_fragmentPagerAdapter;

            viewPager.PageSelected += (sender, args) =>
            {
                if (args.Position == 0)
                {
                    Title = "Credentials";
                    navigationView.SelectedItemId = Resource.Id.action_registration;
                }
                else
                {
                    Title = "Access";
                    navigationView.SelectedItemId = Resource.Id.action_readers;
                }
            };

            navigationView.NavigationItemSelected += (sender, args) =>
            {
                int pageIndex = args.Item.ItemId == Resource.Id.action_registration ? 0 : 1;

                viewPager.ClearFocus();
                viewPager.SetCurrentItem(pageIndex, true);
                viewPager.Invalidate();
                args.Handled = true;
            };
        }
    }
}

