# Maki SL Bot

This will eventually be dockerized, but using WSL for developing works great.

Build using `dotnet build` and find exe in `bin/Debug/net6.0/MakiSLBot`

Download Firestorm for Linux and copy these files to its own directory:

```
bin/SLVoice
lib/libortp.so
lib/libsndfile.so.1
lib/libvivoxoal.so.1
lib/libvivoxplatform.so
lib/libvivoxsdk.so
```

Because SLVoice is 32 bit, you'll need to install some packages on Ubuntu:

```bash
sudo dpkg --add-architecture i386
sudo apt-get install libc6:i386 libstdc++6:i386 zlib1g:i386 libidn11:i386 libuuid1:i386
# unfortunately... but its okay when its dockerized
sudo apt-get install pulseaudio:i386 pulseaudio-utils:i386
# ifconfig is apparently needed somewhere
sudo apt-get install net-tools 
```

Write a `.env` file next to the executable including:

```env
USERNAME=
PASSWORD=
SPAWN=name x y z
SL_VOICE_DIR=
```

