import 'dart:math' as math;
import 'dart:typed_data';

import 'package:audioplayers/audioplayers.dart';

/// Pure decision logic for the kitchen order bell, kept free of audio so it
/// can be unit-tested. The bell rings while at least one ticket still waits
/// for kitchen acceptance ("New") and falls silent once staff accept or
/// progress it — exactly the web Kitchen Display behaviour.
class KitchenAlert {
  KitchenAlert({this.onSignal});

  /// Called with true when the bell should start and false when it must stop.
  final void Function(bool ringing)? onSignal;

  final Set<int> _seenNew = {};
  bool muted = false;
  bool ringing = false;
  int pending = 0;
  List<int> newlyArrived = const [];

  void update(List<dynamic> tickets) {
    final newIds = tickets
        .where((t) => t['status'] == 'New')
        .map((t) => t['id'] as int)
        .toSet();
    pending = newIds.length;
    newlyArrived = newIds.difference(_seenNew).toList()..sort();
    _seenNew
      ..addAll(newIds)
      ..removeWhere((id) => !newIds.contains(id));
    final shouldRing = pending > 0 && !muted;
    if (shouldRing != ringing) {
      ringing = shouldRing;
      onSignal?.call(ringing);
    }
  }

  void setMuted(bool value) {
    if (muted == value) return;
    muted = value;
    final shouldRing = pending > 0 && !muted;
    if (shouldRing != ringing) {
      ringing = shouldRing;
      onSignal?.call(ringing);
    }
  }

  void stop() {
    if (ringing) {
      ringing = false;
      onSignal?.call(false);
    }
    pending = 0;
  }
}

/// One synthesized service-bell strike: inharmonic partials with an
/// exponential decay, wrapped as a mono 16-bit PCM WAV. Generated in code so
/// no audio asset ships with the app and no licensing question arises.
Uint8List buildBellWav({
  double seconds = 1.9,
  int sampleRate = 22050,
  double fundamental = 660,
}) {
  final frames = (seconds * sampleRate).round();
  final samples = Int16List(frames);
  const partials = [(1.0, 0.55), (2.76, 0.28), (5.4, 0.12)];
  for (var i = 0; i < frames; i++) {
    final t = i / sampleRate;
    final decay = math.exp(-3.1 * t);
    var value = 0.0;
    for (final (ratio, amplitude) in partials) {
      value += amplitude * math.sin(2 * math.pi * fundamental * ratio * t);
    }
    samples[i] = (value * decay * 11000).round().clamp(-32768, 32767);
  }
  final header = ByteData(44);
  void ascii(int offset, String text) {
    for (var i = 0; i < text.length; i++) {
      header.setUint8(offset + i, text.codeUnitAt(i));
    }
  }

  final dataSize = frames * 2;
  ascii(0, 'RIFF');
  header.setUint32(4, 36 + dataSize, Endian.little);
  ascii(8, 'WAVE');
  ascii(12, 'fmt ');
  header.setUint32(16, 16, Endian.little);
  header.setUint16(20, 1, Endian.little); // PCM
  header.setUint16(22, 1, Endian.little); // mono
  header.setUint32(24, sampleRate, Endian.little);
  header.setUint32(28, sampleRate * 2, Endian.little);
  header.setUint16(32, 2, Endian.little);
  header.setUint16(34, 16, Endian.little);
  ascii(36, 'data');
  header.setUint32(40, dataSize, Endian.little);
  final out = Uint8List(44 + dataSize);
  out.setAll(0, header.buffer.asUint8List());
  out.setAll(44, samples.buffer.asUint8List());
  return out;
}

/// Loops the synthesized bell while ringing and stops it immediately once
/// kitchen staff accept or progress the order.
class KitchenBell {
  final AudioPlayer _player = AudioPlayer();
  bool _playing = false;

  bool get isPlaying => _playing;

  Future<void> ring() async {
    if (_playing) return;
    _playing = true;
    try {
      await _player.setReleaseMode(ReleaseMode.loop);
      await _player.play(BytesSource(buildBellWav()), volume: 1);
    } catch (_) {
      // Audio unavailable (silent device/emulator): the on-screen banner and
      // count still alert staff, so never break the Kitchen screen.
      _playing = false;
    }
  }

  Future<void> stop() async {
    _playing = false;
    try {
      await _player.stop();
    } catch (_) {}
  }

  Future<void> dispose() async {
    _playing = false;
    try {
      await _player.dispose();
    } catch (_) {}
  }
}
