using Homer.NetDaemon.Apps.LivingRoom;

namespace Homer.NetDaemon.Tests;

public class LivingRoomGoogleHomeTests
{
    private const string Backdrop = "E8C28D3C";
    private const string Spotify = "CC32E753";

    private static readonly TimeSpan LongIdle = TimeSpan.FromHours(3);

    [Fact]
    public void Casts_when_someone_is_around_and_the_hub_has_sat_on_its_photo_frame()
    {
        Assert.True(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", Backdrop, LongIdle));
        Assert.True(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", null, LivingRoomGoogleHome.IdleBeforeCast));
    }

    [Fact]
    public void Leaves_the_hub_to_whoever_just_closed_the_dashboard()
    {
        // The hub's own screens read as "off" too, so someone tapping through them must not be cast over.
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", null, TimeSpan.FromSeconds(5)));
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", null,
            LivingRoomGoogleHome.IdleBeforeCast - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Leaves_the_photo_frame_alone_with_nobody_around()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: false, "off", Backdrop, LongIdle));
    }

    [Fact]
    public void Does_not_cast_again_while_the_dashboard_is_up()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "playing", LivingRoomGoogleHome.DashboardAppId, LongIdle));

        // A display Home Assistant reads as switched away still has the dashboard loaded.
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", LivingRoomGoogleHome.DashboardAppId, LongIdle));
    }

    [Fact]
    public void Does_not_take_over_something_else_on_the_hub()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "playing", Spotify, LongIdle));
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "paused", Spotify, LongIdle));
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "idle", Spotify, LongIdle));
    }

    [Fact]
    public void Waits_for_the_hub_to_come_back_online()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "unavailable", null, LongIdle));
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, null, null, LongIdle));
    }
}
