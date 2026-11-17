:: https://imagemagick.org/script/download.php#windows

set path=%PATH%;c:\Program Files\ImageMagick-7.1.1-Q16-HDRI
magick -background none -density 384 hst-core.svg -trim +repage -resize 1024x1024 -gravity center -extent 1024x1024 PNG32:hst-core.png
magick hst-core.png -resize 512x512 PNG32:hst-core-512x512.png
magick hst-core.png -resize 128x128 PNG32:hst-core-128x128.png
pause
