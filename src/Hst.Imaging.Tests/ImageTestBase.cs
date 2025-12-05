namespace Hst.Imaging.Tests
{
    public abstract class ImageTestBase
    {
        protected Color CreateBlackColor(bool isTransparent)
        {
            return new Color(0, 0, 0, isTransparent ? 0 : 255);
        }
        
        protected Color CreateRedColor()
        {
            return new Color(255, 0, 0);
        }

        protected void SetPixels(Image image, bool isTransparent)
        {
            switch (image.BitsPerPixel)
            {
                case 1:
                    case 2:
                    case 4:
                    case 8:
                    image.SetPixel(0, 0, 1); // pixel 0,0 red
                    image.SetPixel(1, 1, 1); // pixel 1,1 red
                    break;
                case 24:
                    case 32:
                    var blackColor = CreateBlackColor(isTransparent);
                    var redColor = CreateRedColor();
                    
                    image.SetPixel(0, 0, redColor); // pixel 0,0 red
                    image.SetPixel(1, 0, blackColor); // pixel 1,0 black or transparent
                    image.SetPixel(0, 1, blackColor); // pixel 0,1 black or transparent
                    image.SetPixel(1, 1, redColor); // pixel 1,1 red
                    break;
            }
        }

        protected void AddPaletteColors(Image image, bool isTransparent)
        {
            // add black color (color 0)
            image.Palette.AddColor(CreateBlackColor(isTransparent));

            // add red color (color 1)
            image.Palette.AddColor(CreateRedColor());
            
            // set color 0 (black) as transparent color, if is transparent
            image.Palette.TransparentColor = isTransparent ? 0 : -1;
        }
        
        protected Image Create4BppImage(bool isTransparent)
        {
            // create new 2 x 2 image with 4 bits per pixel
            var image = new Image(2, 2, 4);

            // add palette colors
            AddPaletteColors(image, isTransparent);

            // set pixels
            SetPixels(image, isTransparent);
            // image.SetPixel(0, 0, 1); // pixel 0,0 red
            // image.SetPixel(1, 1, 1); // pixel 1,1 red
            
            return image;
        }

        protected Image Create8BppImage(bool isTransparent)
        {
            // create new 2 x 2 image with 8 bits per pixel 
            var image = new Image(2, 2, 8);
            
            // add palette colors
            AddPaletteColors(image, isTransparent);
            
            // set pixels
            SetPixels(image, isTransparent);
            // image.SetPixel(0, 0, 1); // pixel 0,0 red
            // image.SetPixel(1, 1, 1); // pixel 1,1 red

            return image;
        }
        
        protected Image Create24BppImage(bool isTransparent)
        {
            // create new 2 x 2 image with 24 bits per pixel 
            var image = new Image(2, 2, 24);
            
            // set pixels
            SetPixels(image, isTransparent);
            // image.SetPixel(0, 0, 255, 0, 0); // pixel 0,0 red
            // image.SetPixel(1, 0, 0, 0, 0); // pixel 1,0 black or transparent
            // image.SetPixel(0, 1, 0, 0, 0); // pixel 0,1 black or transparent
            // image.SetPixel(1, 1, 255, 0, 0); // pixel 1,1 red

            return image;
        }

        protected Image Create32BppImage(bool isTransparent)
        {
            // create new 2 x 2 image with 32 bits per pixel 
            var image = new Image(2, 2, 32);
            
            // set pixels
            SetPixels(image, isTransparent);
            // image.SetPixel(0, 0, 255, 0, 0); // pixel 0,0 red
            // image.SetPixel(1, 0, 0, 0, 0, isTransparent ? 0 : 255); // pixel 1,0 black or transparent
            // image.SetPixel(0, 1, 0, 0, 0, isTransparent ? 0 : 255); // pixel 0,1 black or transparent
            // image.SetPixel(1, 1, 255, 0, 0); // pixel 1,1 red

            return image;
        }
    }
}