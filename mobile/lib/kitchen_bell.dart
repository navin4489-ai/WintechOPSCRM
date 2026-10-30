import 'dart:async';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Pure decision logic for the kitchen order bell, kept free of audio so it
/// can be unit-tested. The bell rings while at least one ticket still waits
/// for kitchen acceptance ("New") and falls silent once staff accept or
/// progress it — exactly the web Kitchen Display behaviour.
///
/// An [emergency] is declared once a waiting ticket has been ignored for
/// [emergencyAfter] (default 5 minutes) or when at least [emergencyThreshold]
/// tickets are waiting at once. That drives the louder siren.
class KitchenAlert {
  KitchenAlert({
    this.onSignal,
    this.emergencyAfter = const Duration(minutes: 5),
    this.emergencyThreshold = 3,
    this.clock = DateTime.now,
  });

  /// Called with true when the bell should start and false when it must stop.
  /// Mutable so an owner (the alert service) can attach the speaker after
  /// construction.
  void Function(bool ringing)? onSignal;

  /// How long a ticket may wait untouched before the siren takes over.
  final Duration emergencyAfter;

  /// How many simultaneous waiting tickets force the siren immediately.
  final int emergencyThreshold;

  /// Injected so tests can control time.
  final DateTime Function() clock;

  final Set<int> _seenNew = {};
  bool muted = false;
  bool ringing = false;
  bool emergency = false;
  int pending = 0;
  int oldestWaitSeconds = 0;
  List<int> newlyArrived = const [];

  void update(List<dynamic> tickets) {
    final newTickets = tickets.where((t) => t['status'] == 'New').toList();
    final newIds = newTickets.map((t) => t['id'] as int).toSet();
    pending = newIds.length;
    newlyArrived = newIds.difference(_seenNew).toList()..sort();
    _seenNew
      ..addAll(newIds)
      ..removeWhere((id) => !newIds.contains(id));
    oldestWaitSeconds = 0;
    for (final ticket in newTickets) {
      final raw = ticket['createdAt'];
      // A ticket without a parseable timestamp can't age, so it never
      // escalates on its own — only the waiting-count rule can.
      if (raw is! String || raw.isEmpty) continue;
      final created = DateTime.tryParse(raw);
      if (created == null) continue;
      final waited = clock().difference(created).inSeconds;
      if (waited > oldestWaitSeconds) oldestWaitSeconds = waited;
    }
    emergency = pending > 0 &&
        (pending >= emergencyThreshold ||
            oldestWaitSeconds >= emergencyAfter.inSeconds);
    // Muting silences the speaker but must not hide an emergency — the red
    // banner keeps warning until the backlog is actually handled.
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
    emergency = false;
    oldestWaitSeconds = 0;
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
  return wrapAsPcmWav(samples, sampleRate);
}

/// A two-tone emergency siren: the pitch ramps up and down repeatedly so it is
/// unmistakable against the normal service bell and audible across a kitchen.
Uint8List buildSirenWav({
  double seconds = 2.4,
  int sampleRate = 22050,
  double low = 520,
  double high = 1180,
}) {
  final frames = (seconds * sampleRate).round();
  final samples = Int16List(frames);
  const cycles = 3.0;
  for (var i = 0; i < frames; i++) {
    final t = i / sampleRate;
    final phase = (t * cycles) % 1.0;
    // Triangle sweep between the two tones, plus a hard on/off pulse so the
    // siren has a clear repeating rhythm.
    final sweep = phase < 0.5 ? phase * 2 : 2 - phase * 2;
    final freq = low + (high - low) * sweep;
    final tone = math.sin(2 * math.pi * freq * t);
    final pulse = phase < 0.82 ? 1.0 : 0.15;
    samples[i] = (tone * pulse * 12500).round().clamp(-32768, 32767);
  }
  return wrapAsPcmWav(samples, sampleRate);
}

/// Shared mono 16-bit PCM WAV container used by every synthesized sound.
Uint8List wrapAsPcmWav(Int16List samples, int sampleRate) {
  final frames = samples.length;
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

/// Loops the synthesized bell (or the emergency siren) and stops immediately
/// once kitchen staff accept or progress the order.
class KitchenBell {
  final AudioPlayer _player = AudioPlayer();
  bool _playing = false;
  bool _emergency = false;

  bool get isPlaying => _playing;
  bool get isEmergency => _emergency;

  Future<void> ring({bool emergency = false}) async {
    if (_playing && _emergency == emergency) return;
    _playing = true;
    _emergency = emergency;
    try {
      await _player.setReleaseMode(ReleaseMode.loop);
      await _player.play(
        BytesSource(emergency ? buildSirenWav() : buildBellWav()),
        volume: 1,
      );
    } catch (_) {
      // Audio unavailable (silent device/emulator): the on-screen banner and
      // count still alert staff, so never break the Kitchen screen.
      _playing = false;
      _emergency = false;
    }
  }

  /// Plays one full-length emergency siren pass, then stops (used for the
  /// manual "test emergency ring" button).
  Future<void> ringOnce({bool emergency = false}) async {
    await stop();
    try {
      await _player.setReleaseMode(ReleaseMode.stop);
      await _player.play(
        BytesSource(emergency ? buildSirenWav() : buildBellWav()),
        volume: 1,
      );
    } catch (_) {}
  }

  Future<void> stop() async {
    _playing = false;
    _emergency = false;
    try {
      await _player.stop();
    } catch (_) {}
  }

  Future<void> dispose() async {
    _playing = false;
    _emergency = false;
    try {
      await _player.dispose();
    } catch (_) {}
  }
}

/// App-wide kitchen alert, independent of which screen is open.
///
/// Polls the Kitchen Tickets endpoint for the signed-in outlet every few
/// seconds, rings the bell for new orders and escalates to the siren when a
/// ticket has waited too long. Staff can mute it; the mute choice survives an
/// app restart. The ticket fetch and secure storage are injected so the whole
/// escalation flow can be unit-tested without platform channels.
class KitchenAlertService extends ChangeNotifier {
  KitchenAlertService({
    required Future<List<dynamic>> Function() fetchTickets,
    this.pollInterval = const Duration(seconds: 5),
    FlutterSecureStorage? secureStorage,
    DateTime Function()? clock,
  }) : _fetch = fetchTickets,
       _storage = secureStorage,
       _alert = KitchenAlert(clock: clock ?? DateTime.now) {
    // Wired here rather than in the initializer list: a callback passed to a
    // field initializer cannot touch `this`, and this one drives the speaker.
    _alert.onSignal = (_) => _syncAudio();
  }

  final Future<List<dynamic>> Function() _fetch;
  final Duration pollInterval;
  final FlutterSecureStorage? _storage;
  final KitchenAlert _alert;
  final KitchenBell _bell = KitchenBell();

  static const String _mutedKey = 'win_kitchen_bell_muted';

  Timer? _timer;
  bool _busy = false;
  bool _running = false;
  String? lastError;

  bool get running => _running;
  bool get ringing => _alert.ringing;
  bool get emergency => _alert.emergency;
  bool get muted => _alert.muted;
  int get pending => _alert.pending;
  int get oldestWaitSeconds => _alert.oldestWaitSeconds;
  String get stateLabel => !running
      ? 'off'
      : emergency
      ? 'emergency'
      : ringing
      ? 'ringing'
      : muted && pending > 0
      ? 'muted'
      : 'silent';

  /// Load the persisted mute choice, then start polling. Safe to call twice.
  Future<void> start() async {
    if (_running) return;
    _running = true;
    try {
      final stored = await _storage?.read(key: _mutedKey);
      _alert.setMuted(stored == '1');
    } catch (_) {
      // Storage unavailable: keep the default (unmuted).
    }
    notifyListeners();
    _timer = Timer.periodic(pollInterval, (_) => refresh());
    await refresh();
  }

  /// Stop polling and silence any sound. Called on sign-out.
  Future<void> stop() async {
    _timer?.cancel();
    _timer = null;
    _running = false;
    _alert.stop();
    _syncAudio();
    lastError = null;
    notifyListeners();
  }

  /// Pull the current tickets and update bell/emergency state.
  Future<void> refresh() async {
    if (_busy || !_running) return;
    _busy = true;
    try {
      final rows = await _fetch();
      if (!_running) return;
      _alert.update(rows);
      lastError = null;
    } catch (e) {
      // A transient network failure must never silence a ringing bell.
      lastError = e.toString();
    } finally {
      _busy = false;
      _syncAudio();
      notifyListeners();
    }
  }

  Future<void> setMuted(bool value) async {
    _alert.setMuted(value);
    _syncAudio();
    notifyListeners();
    try {
      await _storage?.write(key: _mutedKey, value: value ? '1' : '0');
    } catch (_) {}
  }

  /// Manual sound check so staff can confirm the device is audible.
  Future<void> testRing({bool emergency = false}) async {
    HapticFeedback.heavyImpact();
    if (emergency) {
      await _bell.ringOnce(emergency: true);
    } else {
      await _bell.ring();
      await Future<void>.delayed(const Duration(seconds: 4));
      if (!_alert.ringing) await _bell.stop();
    }
  }

  Future<void> disposeService() async {
    await stop();
    await _bell.dispose();
  }

  /// Match the speaker to the current alert level: siren while an emergency
  /// is declared, plain bell for a normal wait, silence otherwise.
  void _syncAudio() {
    if (!_alert.ringing) {
      _bell.stop();
    } else {
      _bell.ring(emergency: _alert.emergency);
    }
  }
}
