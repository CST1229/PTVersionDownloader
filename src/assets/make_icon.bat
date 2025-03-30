@echo off
where /q magick
if ERRORLEVEL 1 (
	echo This script requires ImageMagick installed as `magick` to run!
	pause
) else (
	magick convert icon_16x16.png icon_32x32.png icon_48x48.png icon_64x64.png icon_128x128.png icon_256x256.png -compress Zip icon.ico
)