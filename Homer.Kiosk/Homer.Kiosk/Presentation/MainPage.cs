namespace Homer.Kiosk.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.DataContext<MainViewModel>((page, vm) => page
            .NavigationCacheMode(NavigationCacheMode.Required)
            .Background(Theme.Brushes.Background.Default)
            .Content(new Grid()
                .SafeArea(SafeArea.InsetMask.VisibleBounds)
                .RowDefinitions("Auto,Auto,*")
                .Padding(8)
                .Children(
                    // Top Bar
                    new TextBlock()
                        .Grid(row: 0)
                        .Text(() => vm.Title)
                        .FontSize(24)
                        .FontWeight(Microsoft.UI.Text.FontWeights.Bold)
                        .Margin(0, 0, 0, 16),

                    // Power Buttons Row
                    new Grid()
                        .Grid(row: 1)
                        .ColumnDefinitions("*,*,*")
                        .Height(200)
                        .Margin(0, 0, 0, 16)
                        .Children(
                            // Balcony Lights Button
                            CreatePowerButton("阳台灯", 0),
                            // Air Con 1 Button
                            CreatePowerButton("空调一", 1),
                            // Air Con 2 Button
                            CreatePowerButton("空调二", 2)
                        ),

                    // Additional Info Section
                    new StackPanel()
                        .Grid(row: 2)
                        .Spacing(16)
                        .Children(
                            new TextBlock()
                                .Text("Weather & Bus Information")
                                .FontSize(18)
                                .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold),
                            
                            new Grid()
                                .ColumnDefinitions("*,*")
                                .Children(
                                    // Weather Card
                                    new Border()
                                        .Grid(column: 0)
                                        .Background(Theme.Brushes.Surface.Default)
                                        .CornerRadius(8)
                                        .Padding(16)
                                        .Margin(0, 0, 8, 0)
                                        .Child(
                                            new StackPanel()
                                                .Spacing(8)
                                                .Children(
                                                    new TextBlock()
                                                        .Text("天气")
                                                        .FontSize(16)
                                                        .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold),
                                                    new TextBlock()
                                                        .Text("Weather information will appear here")
                                                        .Opacity(0.7)
                                                )
                                        ),
                                    
                                    // Bus Card
                                    new Border()
                                        .Grid(column: 1)
                                        .Background(Theme.Brushes.Surface.Default)
                                        .CornerRadius(8)
                                        .Padding(16)
                                        .Margin(8, 0, 0, 0)
                                        .Child(
                                            new StackPanel()
                                                .Spacing(8)
                                                .Children(
                                                    new TextBlock()
                                                        .Text("巴士")
                                                        .FontSize(16)
                                                        .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold),
                                                    new TextBlock()
                                                        .Text("Bus timing information will appear here")
                                                        .Opacity(0.7)
                                                )
                                        )
                                )
                        )
                )));
    }

    private Border CreatePowerButton(string name, int column)
    {
        return new Border()
            .Grid(column: column)
            .Margin(4)
            .CornerRadius(12)
            .Child(
                new Button()
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .VerticalAlignment(VerticalAlignment.Stretch)
                    .Content(
                        new Grid()
                            .RowDefinitions("*,Auto")
                            .Padding(16)
                            .Children(
                                new TextBlock()
                                    .Grid(row: 0)
                                    .Text(name)
                                    .FontSize(20)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new Ellipse()
                                    .Grid(row: 1)
                                    .Width(8)
                                    .Height(8)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Fill(new SolidColorBrush(Microsoft.UI.Colors.LightGray))
                            )
                    )
            );
    }
}
