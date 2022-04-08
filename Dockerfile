FROM bitnami/dotnet-sdk:latest AS builder

WORKDIR /build

RUN \
mkdir -p ActualSLVoice && \
# download and extract firestorm
wget https://downloads.firestormviewer.org/linux/Phoenix_Firestorm-Releasex64_x86_64_6.5.3.65658.tar.xz && \
tar -xf Phoenix_Firestorm-Releasex64_x86_64_6.5.3.65658.tar.xz && \
rm -f Phoenix_Firestorm-Releasex64_x86_64_6.5.3.65658.tar.xz && \
cd Phoenix_Firestorm-Releasex64_x86_64_6.5.3.65658 && \
# copy slvoice things
cp bin/SLVoice /build/ActualSLVoice/ && \
cp lib/libortp.so /build/ActualSLVoice/ && \
cp lib/libsndfile.so.1 /build/ActualSLVoice/ && \
cp lib/libvivoxoal.so.1 /build/ActualSLVoice/ && \
cp lib/libvivoxplatform.so /build/ActualSLVoice/ && \
cp lib/libvivoxsdk.so /build/ActualSLVoice/ && \
# cleanup
cd /build && \
rm -rf Phoenix_Firestorm-Releasex64_x86_64_6.5.3.65658

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
dpkg --add-architecture i386 && \
apt-get update -y && \
apt-get install -y \
libc6:i386 libstdc++6:i386 zlib1g:i386 libidn11:i386 libuuid1:i386 \
pulseaudio:i386 pulseaudio-utils:i386 \
net-tools 

COPY --from=builder /build/MakiSLBot/bin/Release/net6.0/linux-x64/publish/ /app/
COPY --from=builder /build/ActualSLVoice /app/ActualSLVoice

ENV SL_VOICE_DIR=/app/ActualSLVoice

CMD /app/MakiSLBot