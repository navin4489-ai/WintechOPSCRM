import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:oro_pos_app/kitchen_bell.dart';

Map<String, dynamic> ticket(int id, String status) => {
  'id': id,
  'orderNumber': 'ORD-$id',
  'stationName': 'Hot kitchen',
  'status': status,
};

void main() {
  group('KitchenAlert', () {
    test('Rings while an order waits and stops when staff accept it', () {
      final signals = <bool>[];
      final alert = KitchenAlert(onSignal: signals.add);

      alert.update([ticket(1, 'New')]);
      expect(alert.ringing, isTrue);
      expect(alert.pending, 1);

      // Staff accept the order: the ring must stop immediately.
      alert.update([ticket(1, 'Accepted')]);
      expect(alert.ringing, isFalse);
      expect(alert.pending, 0);
      expect(signals, [true, false]);
    });

    test('Keeps ringing until every waiting order is accepted', () {
      final alert = KitchenAlert();
      alert.update([ticket(1, 'New'), ticket(2, 'New')]);
      expect(alert.ringing, isTrue);
      expect(alert.pending, 2);

      alert.update([ticket(1, 'Accepted'), ticket(2, 'New')]);
      expect(alert.ringing, isTrue);
      expect(alert.pending, 1);

      alert.update([ticket(1, 'Accepted'), ticket(2, 'Ready')]);
      expect(alert.ringing, isFalse);
    });

    test('Repeated polls with the same New ticket do not re-signal', () {
      final signals = <bool>[];
      final alert = KitchenAlert(onSignal: signals.add);
      alert.update([ticket(7, 'New')]);
      alert.update([ticket(7, 'New')]);
      alert.update([ticket(7, 'New')]);
      expect(signals, [true], reason: 'ring only once per arrival');
      expect(alert.newlyArrived, isEmpty, reason: 'already announced');
    });

    test('Mute silences the bell but keeps the waiting count visible', () {
      final signals = <bool>[];
      final alert = KitchenAlert(onSignal: signals.add);
      alert.update([ticket(1, 'New')]);
      alert.setMuted(true);
      expect(alert.ringing, isFalse);
      expect(alert.pending, 1);
      expect(alert.muted, isTrue);

      // A second order while muted must not start the bell.
      alert.update([ticket(1, 'New'), ticket(2, 'New')]);
      expect(alert.ringing, isFalse);
      expect(alert.pending, 2);

      alert.setMuted(false);
      expect(alert.ringing, isTrue, reason: 'unmuting resumes the ring');
      expect(signals, [true, false, true]);
    });

    test('Fresh order after the queue cleared rings again', () {
      final signals = <bool>[];
      final alert = KitchenAlert(onSignal: signals.add);
      alert.update([ticket(1, 'New')]);
      alert.update([ticket(1, 'Served')]);
      alert.update([ticket(1, 'Served'), ticket(2, 'New')]);
      expect(alert.ringing, isTrue);
      expect(alert.newlyArrived, [2]);
      expect(signals, [true, false, true]);
    });

    test('stop() silences an active ring and clears the count', () {
      final alert = KitchenAlert();
      alert.update([ticket(3, 'New')]);
      alert.stop();
      expect(alert.ringing, isFalse);
      expect(alert.pending, 0);
    });
  });

  group('KitchenAlert emergency escalation', () {
    final fixedNow = DateTime(2026, 1, 1, 12, 0, 0);
    Map<String, dynamic> aged(int id, int secondsOld) => {
      'id': id,
      'orderNumber': 'ORD-$id',
      'status': 'New',
      'createdAt': fixedNow.subtract(Duration(seconds: secondsOld)).toIso8601String(),
    };

    test('A fresh order rings the normal bell, not the siren', () {
      final alert = KitchenAlert(clock: () => fixedNow);
      alert.update([aged(1, 30)]);
      expect(alert.ringing, isTrue);
      expect(alert.emergency, isFalse);
    });

    test('Escalates to emergency once a ticket waits 5 minutes', () {
      final alert = KitchenAlert(clock: () => fixedNow);
      alert.update([aged(1, 4 * 60)]);
      expect(alert.emergency, isFalse, reason: 'still inside the grace window');

      alert.update([aged(1, 5 * 60 + 1)]);
      expect(alert.emergency, isTrue, reason: 'ignored too long — siren');
      expect(alert.oldestWaitSeconds, greaterThanOrEqualTo(300));
    });

    test('Three or more waiting orders escalate immediately', () {
      final alert = KitchenAlert(clock: () => fixedNow);
      alert.update([aged(1, 5), aged(2, 4), aged(3, 3)]);
      expect(alert.pending, 3);
      expect(alert.emergency, isTrue, reason: 'a pile-up forces the siren');
    });

    test('Emergency clears as soon as the backlog is handled', () {
      final alert = KitchenAlert(clock: () => fixedNow);
      alert.update([aged(1, 10 * 60)]);
      expect(alert.emergency, isTrue);

      alert.update([{'id': 1, 'status': 'Accepted', 'createdAt': aged(1, 10 * 60)['createdAt']}]);
      expect(alert.emergency, isFalse);
      expect(alert.ringing, isFalse);
    });

    test('A ticket with no createdAt never escalates on age', () {
      final alert = KitchenAlert(clock: () => fixedNow);
      alert.update([ticket(1, 'New')]);
      expect(alert.ringing, isTrue);
      expect(alert.emergency, isFalse);
      expect(alert.oldestWaitSeconds, 0);
    });

    test('Mute silences even during an emergency', () {
      final alert = KitchenAlert(clock: () => fixedNow);
      alert.update([aged(1, 10 * 60)]);
      expect(alert.emergency, isTrue);
      expect(alert.ringing, isTrue);

      alert.setMuted(true);
      expect(alert.ringing, isFalse, reason: 'muted means silent');
      expect(alert.emergency, isTrue, reason: 'the red banner still warns');
    });
  });

  group('buildSirenWav', () {
    test('Produces a valid WAV distinct from the normal bell', () {
      final bytes = buildSirenWav(seconds: 0.5, sampleRate: 8000);
      expect(bytes.sublist(0, 4), 'RIFF'.codeUnits);
      expect(bytes.sublist(8, 12), 'WAVE'.codeUnits);
      final view = ByteData.sublistView(bytes);
      expect(view.getUint32(24, Endian.little), 8000);
      final dataSize = view.getUint32(40, Endian.little);
      expect(dataSize, (0.5 * 8000).round() * 2);

      // The siren is loud and does not decay away like the bell strike.
      final peak = _peak(bytes, from: 44, to: bytes.length);
      expect(peak, greaterThan(5000), reason: 'the siren is unmistakable');

      // Zero crossings reveal the pitch sweeping between two tones.
      final crossings = _zeroCrossings(bytes, from: 44, to: 44 + 4000 * 2);
      final bellCrossings = _zeroCrossings(
        buildBellWav(seconds: 0.5, sampleRate: 8000),
        from: 44,
        to: 44 + 4000 * 2,
      );
      expect(crossings, greaterThan(bellCrossings), reason: 'siren is higher pitched');
    });
  });

  group('buildBellWav', () {
    test('Produces a valid mono 16-bit PCM WAV with a decaying strike', () {
      final bytes = buildBellWav(seconds: 0.5, sampleRate: 8000);
      expect(bytes.sublist(0, 4), 'RIFF'.codeUnits);
      expect(bytes.sublist(8, 12), 'WAVE'.codeUnits);
      expect(bytes.sublist(12, 16), 'fmt '.codeUnits);
      expect(bytes.sublist(36, 40), 'data'.codeUnits);

      final view = ByteData.sublistView(bytes);
      expect(view.getUint16(20, Endian.little), 1, reason: 'PCM format');
      expect(view.getUint16(22, Endian.little), 1, reason: 'mono');
      expect(view.getUint32(24, Endian.little), 8000);
      expect(view.getUint16(34, Endian.little), 16, reason: '16-bit');
      final dataSize = view.getUint32(40, Endian.little);
      expect(dataSize, (0.5 * 8000).round() * 2, reason: 'frames × 2 bytes');
      expect(bytes.length, 44 + dataSize);
      expect(view.getUint32(4, Endian.little), 36 + dataSize);

      final firstMs = _peak(bytes, from: 44, to: 44 + 80 * 2);
      final lastMs = _peak(
        bytes,
        from: bytes.length - 80 * 2,
        to: bytes.length,
      );
      expect(firstMs, greaterThan(1000), reason: 'the strike is audible');
      expect(lastMs, lessThan(firstMs ~/ 4), reason: 'sound decays to silence');
    });
  });
}

int _zeroCrossings(Uint8List bytes, {required int from, required int to}) {
  final view = ByteData.sublistView(bytes);
  var count = 0;
  var previous = view.getInt16(from, Endian.little);
  for (var i = from + 2; i < to; i += 2) {
    final sample = view.getInt16(i, Endian.little);
    if ((previous >= 0) != (sample >= 0)) count++;
    previous = sample;
  }
  return count;
}

int _peak(Uint8List bytes, {required int from, required int to}) {
  final view = ByteData.sublistView(bytes);
  var peak = 0;
  for (var i = from; i < to; i += 2) {
    final sample = view.getInt16(i, Endian.little).abs();
    if (sample > peak) peak = sample;
  }
  return peak;
}
