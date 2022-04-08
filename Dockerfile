FROM bitnami/dotnet-sdk:latest AS builder
ENV DEBIAN_FRONTEND=noninteractive

WORKDIR /build

RUN \
apt-get update -y && \
apt-get install -y p7zip-full && \
# download and extract second life \
mkdir -p ActualSLVoice && \
mkdir -p SecondLife && \
cd SecondLife && \
wget https://viewer-download.secondlife.com/Viewer_6/Second_Life_6_5_3_568554_x86_64_Setup.exe && \
7z x -y Second_Life_6_5_3_568554_x86_64_Setup.exe && \
# copy slvoice things
cp SLVoice.exe /build/ActualSLVoice/ && \
cp ortp_x64.dll /build/ActualSLVoice/ && \
cp vivoxsdk_x64.dll /build/ActualSLVoice/ && \
# cleanup
cd /build && \
rm -rf SecondLife

COPY MakiSLBot /build/MakiSLBot/
COPY MakiSLBot.sln /build/MakiSLBot.sln

RUN \
cd /build/MakiSLBot/ && \
dotnet publish -c Release -r linux-x64 --self-contained

# ---

FROM ubuntu:latest
ENV DEBIAN_FRONTEND=noninteractive

WORKDIR /app

RUN \
apt-get update -y && \
apt-get install -y wine pulseaudio pulseaudio-utils net-tools winetricks && \
# run wine at least once so it doesnt have to on runtime (happens below)
# wine64 regsvr32; exit 0 \
# we need to install this or else SLVoice wont work. command fails but works anyway lol
# https://forum.winehq.org/viewtopic.php?t=30682
winetricks -q mdac28 || true && \
# cleanup
apt-get purge -y winetricks && \
SUDO_FORCE_REMOVE=yes apt-get autoremove -y --purge && \
apt-get clean -y 

COPY --from=builder /build/MakiSLBot/bin/Release/net6.0/linux-x64/publish/ /app/
COPY --from=builder /build/ActualSLVoice /app/ActualSLVoice

ENV SL_VOICE_DIR=/app/ActualSLVoice

CMD /app/MakiSLBot