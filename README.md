# Maki SL Bot

## Deploying

Copy `docker-compose.example.yml` to `docker-compose.yml` and fill out the details

Run `docker-compose up -d` to start, `docker-compose down` to stop

It'll build the first time. If you want to manually rebuild run `docker-compose build`

## Building

Make sure you're working in WSL or you won't be able to use PulseAudio.

Build using `dotnet build` and find exe in `bin/Debug/net6.0/MakiSLBot`

Copy these files your Windows viewer installation to its own directory:

```
SLVoice.exe
ortp_x64.dll
vivoxsdk_x64.dll
```

Install these packages on Ubuntu:

```bash
sudo apt-get update -y
sudo apt-get install wine pulseaudio pulseaudio-utils net-tools
```

Write a `.env` file next to the executable including:

```env
FIRST_NAME=
LAST_NAME=
PASSWORD=
SPAWN=name x y z
SL_VOICE_DIR=
OWNER_USERNAME=
```

You might want to run `killall pulseaudio` every once in a while if it doesn't clean up properly.