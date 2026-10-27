import 'package:flutter/material.dart';

const navy = Color(0xff0b1b3a);
const gold = Color(0xffffb515);
const canvas = Color(0xfff0f3f8);

class OroLogo extends StatelessWidget {
  final double size;
  const OroLogo({super.key, this.size = 48});
  @override
  Widget build(BuildContext context) => Semantics(
    label: 'ORO POS logo',
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
      child: CustomPaint(painter: _OroMark()),
    ),
  );
}

class _OroMark extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = navy
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * .09
      ..strokeCap = StrokeCap.round;
    canvas.drawArc(
      Rect.fromCircle(
        center: Offset(size.width / 2, size.height / 2),
        radius: size.width * .235,
      ),
      -.6,
      5.3,
      false,
      paint,
    );
    canvas.drawCircle(
      Offset(size.width * .69, size.height * .30),
      size.width * .055,
      Paint()..color = navy,
    );
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
