# ShaderMotion

This project implements a shader-based Mecanim motion encoder/decoder in Unity 2018. The motion encoder (recorder) takes one avatar and outputs encoded Mecanim muscle values to a motion texture. The motion decoder (replayer) takes a motion texture and skins another avatar with the decoded muscle values. The encoder/decoder needs to be pre-generated in editor script, but the encoding/decoding process is completely done in shader.

This project is intended for streaming 3d avatar motion in VRChat. The streamer wears an avatar with encoder in any world, and broadcasts the encoded motion video. The viewers can play the motion video in any world with video player, and view the streamer's motion through a decoder, which may be provided by either the world or even the viewer's own avatar!

# How to use

Please read [Wiki](../../wikis/home)