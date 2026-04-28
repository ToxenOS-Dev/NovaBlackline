using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NovaBlackline;

public partial class MainWindow
{
    const int ShopTilesPerRow = 4;

    void OpenShop()
    {
        _layer          = Layer.Shop;
        _shopTabIndex   = 0;
        _shopInContent  = false;
        _shopGameIndex  = 0;
        ShopOverlay.IsVisible = true;
        DrawShopTabs();
        DrawShopContent();
        UpdateShopFooter();
    }

    void CloseShop()
    {
        _layer = Layer.Tiles;
        ShopOverlay.IsVisible = false;
    }

    void HandleShopKey(Key key)
    {
        if (_shopInContent)
        {
            switch (key)
            {
                case Key.Left when _shopGameIndex > 0:
                    _shopGameIndex--; DrawShopContent(); break;
                case Key.Right when _shopGameIndex < FeaturedGames.Length - 1:
                    _shopGameIndex++; DrawShopContent(); break;
                case Key.Up:
                    MoveShopContentUp(); break;
                case Key.Down:
                    MoveShopContentDown(); break;
                case Key.Enter:
                    LaunchFeaturedGame(_shopGameIndex); break;
                case Key.Escape:
                    _shopInContent = false; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); break;
            }
        }
        else
        {
            switch (key)
            {
                case Key.Left when _shopTabIndex > 0:
                    _shopTabIndex--; _shopGameIndex = 0; DrawShopTabs(); DrawShopContent(); break;
                case Key.Right when _shopTabIndex < ShopTabs.Count - 1:
                    _shopTabIndex++; _shopGameIndex = 0; DrawShopTabs(); DrawShopContent(); break;
                case Key.Down when ShopTabs[_shopTabIndex].Name == "Nova Shop":
                    EnterShopContent(); break;
                case Key.Enter:
                    if (ShopTabs[_shopTabIndex].Name == "Nova Shop") EnterShopContent();
                    else ActivateCurrentShopTab();
                    break;
                case Key.Escape: CloseShop(); break;
            }
        }
    }

    void EnterShopContent()
    {
        _shopInContent = true;
        DrawShopTabs();
        DrawShopContent();
        UpdateShopFooter();
    }

    void MoveShopContentUp()
    {
        if (_shopGameIndex < ShopTilesPerRow)
        {
            _shopInContent = false;
            DrawShopTabs();
            DrawShopContent();
            UpdateShopFooter();
        }
        else
        {
            _shopGameIndex -= ShopTilesPerRow;
            DrawShopContent();
        }
    }

    void MoveShopContentDown()
    {
        int next = _shopGameIndex + ShopTilesPerRow;
        if (next < FeaturedGames.Length)
        {
            _shopGameIndex = next;
        }
        else if (_shopGameIndex / ShopTilesPerRow < (FeaturedGames.Length - 1) / ShopTilesPerRow)
        {
            _shopGameIndex = FeaturedGames.Length - 1;
        }
        DrawShopContent();
    }

    void UpdateShopFooter()
    {
        ShopFooterHint.Text = _shopInContent
            ? "← → ↑ ↓  navigate        ENTER  launch        ↑ (first row)  back to tabs        ESC  close"
            : "← →  switch tab        ↓  browse games        ENTER  open        ESC  close";
    }

    void DrawShopTabs()
    {
        ShopTabBar.Children.Clear();
        for (int i = 0; i < ShopTabs.Count; i++)
        {
            bool sel = i == _shopTabIndex;
            int  idx = i;
            var tab = new Border
            {
                Padding         = new Thickness(22, 0),
                Background      = sel
                    ? new SolidColorBrush(Color.FromArgb(40, _accent.R, _accent.G, _accent.B))
                    : Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, sel ? 2 : 0),
                BorderBrush     = new SolidColorBrush(sel ? _accent : Colors.Transparent),
                Cursor          = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock
                {
                    Text              = ShopTabs[i].Name,
                    FontSize          = 14,
                    FontWeight        = sel ? FontWeight.Bold : FontWeight.Normal,
                    Foreground        = sel
                        ? new SolidColorBrush(_accent)
                        : new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                }
            };
            tab.PointerPressed += (_, _) => { _shopTabIndex = idx; DrawShopTabs(); DrawShopContent(); };
            ShopTabBar.Children.Add(tab);
        }
    }

    void DrawShopContent()
    {
        ShopContent.Children.Clear();
        var shop = ShopTabs[_shopTabIndex];

        if (shop.Name == "Nova Shop")
        {
            DrawNovaShopContent();
        }
        else
        {
            DrawExternalShopContent(shop);
        }
    }

    void DrawNovaShopContent()
    {
        ShopContent.Children.Add(new TextBlock
        {
            Text       = "FREE & FEATURED",
            FontSize   = 12,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(_accent),
            Margin     = new Thickness(0, 0, 0, 16),
        });

        var wrap = new WrapPanel();
        for (int i = 0; i < FeaturedGames.Length; i++)
        {
            var (name, appId, color) = FeaturedGames[i];
            bool sel = _shopInContent && i == _shopGameIndex;
            string aid = appId;
            int    idx = i;

            var tile = new Border
            {
                Width           = 190,
                Height          = 110,
                Background      = new SolidColorBrush(color),
                CornerRadius    = new CornerRadius(8),
                Margin          = new Thickness(0, 0, 12, 12),
                Cursor          = new Cursor(StandardCursorType.Hand),
                BorderThickness = new Thickness(sel ? 3 : 0),
                BorderBrush     = new SolidColorBrush(_accent),
                Opacity         = !_shopInContent || sel ? 1.0 : 0.55,
                Child = new TextBlock
                {
                    Text              = name,
                    FontSize          = 13,
                    FontWeight        = FontWeight.Bold,
                    Foreground        = Brushes.White,
                    TextWrapping      = TextWrapping.Wrap,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                    Margin            = new Thickness(12, 0, 12, 12),
                }
            };
            tile.PointerPressed += (_, _) =>
            {
                _shopInContent = true;
                _shopGameIndex = idx;
                Launch(new LaunchItem("", "", "", $"xdg-open steam://store/{aid}", default));
            };
            wrap.Children.Add(tile);
        }
        ShopContent.Children.Add(wrap);
    }

    void DrawExternalShopContent(ShopEntry shop)
    {
        ShopContent.Children.Add(new TextBlock
        {
            Text       = shop.Name.ToUpper(),
            FontSize   = 30,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(_accent),
            Margin     = new Thickness(0, 16, 0, 8),
        });
        ShopContent.Children.Add(new TextBlock
        {
            Text       = shop.Description,
            FontSize   = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
            Margin     = new Thickness(0, 0, 0, 32),
        });
        ShopContent.Children.Add(new TextBlock
        {
            Text       = "Press ENTER to open  →",
            FontSize   = 15,
            Foreground = new SolidColorBrush(_accent),
        });
    }

    void ActivateCurrentShopTab()
    {
        var shop = ShopTabs[_shopTabIndex];
        if (!string.IsNullOrEmpty(shop.Command))
            Launch(new LaunchItem(shop.Name, "", "", shop.Command, default));
    }

    void LaunchFeaturedGame(int index)
    {
        var (_, appId, _) = FeaturedGames[index];
        Launch(new LaunchItem("", "", "", $"xdg-open steam://store/{appId}", default));
    }
}
