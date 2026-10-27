import 'package:flutter/material.dart';

const navy = Color(0xff0b1b3a);
const gold = Color(0xffffb515);
const canvas = Color(0xfff0f3f8);

class WintechLogo extends StatelessWidget {
  final double size;
  const WintechLogo({super.key, this.size = 48});
  @override
  Widget build(BuildContext context) => Semantics(
    label: 'Wintech POS logo',
    image: true,
    child: Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: gold,
        borderRadius: BorderRadius.circular(size * .28),
        boxShadow: [
          BoxShadow(
            color: gold.withValues(alpha: .18),
            blurRadius: 22,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: CustomPaint(painter: _WintechMark()),
    ),
  );
}

class _WintechMark extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = navy
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * .09
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    final path = Path()
      ..moveTo(size.width * .211, size.height * .297)
      ..lineTo(size.width * .322, size.height * .703)
      ..lineTo(size.width * .5, size.height * .449)
      ..lineTo(size.width * .678, size.height * .703)
      ..lineTo(size.width * .789, size.height * .297);
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
