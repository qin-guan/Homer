using Homer.NetDaemon.Apps.LivingRoom;

namespace Homer.NetDaemon.Tests;

public class LivingRoomGoogleHomeTests
{
    private const string Backdrop = "E8C28D3C";
    private const string Spotify = "CC32E753";

    [Fact]
    public void Casts_when_someone_is_around_and_the_hub_shows_its_photo_frame()
    {
        Assert.True(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", Backdrop));
        Assert.True(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", null));
    }

    [Fact]
    public void Leaves_the_photo_frame_alone_with_nobody_around()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: false, "off", Backdrop));
    }

    [Fact]
    public void Does_not_cast_again_while_the_dashboard_is_up()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "playing", LivingRoomGoogleHome.DashboardAppId));

        // A display Home Assistant reads as switched away still has the dashboard loaded.
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "off", LivingRoomGoogleHome.DashboardAppId));
    }

    [Fact]
    public void Does_not_take_over_something_else_on_the_hub()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "playing", Spotify));
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "paused", Spotify));
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "idle", Spotify));
    }

    [Fact]
    public void Waits_for_the_hub_to_come_back_online()
    {
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, "unavailable", null));
        Assert.False(LivingRoomGoogleHome.ShouldCast(occupied: true, null, null));
    }
}
