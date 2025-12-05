use crate::profile::profiles::{CurveParams, ResponseCurve};

// Lookup table resolution for custom curves (256 entries ~1KB).
const LUT_SIZE: usize = 256;

/// Trait for applying a response curve.
pub trait CurveProcessor {
    fn process_input(&self, raw_value: f32) -> f32;
}

/// Curve implementation supporting linear and custom response curves.
#[derive(Debug, Clone)]
pub struct UnifiedCurve {
    pub curve_type: ResponseCurve,
    pub params: CurveParams,
    pub dead_zone_inner: f32,
    pub dead_zone_outer: f32,
    // Pre-computed lookup table for custom curves.
    lut: Option<Box<[f32; LUT_SIZE]>>,
}

// Interpolation functions
#[inline(always)]
fn linear_interp(t: f32, y0: f32, delta: f32) -> f32 {
    y0 + t * delta
}

#[inline(always)]
fn hermite_interp(t: f32, p0: f32, p1: f32, m0: f32, m1: f32, dx: f32) -> f32 {
    let t2 = t * t;
    let t3 = t2 * t;

    let h00 = 2.0 * t3 - 3.0 * t2 + 1.0;
    let h10 = t3 - 2.0 * t2 + t;
    let h01 = -2.0 * t3 + 3.0 * t2;
    let h11 = t3 - t2;

    // Interpolate with tangents scaled by segment length
    h00 * p0 + h10 * dx * m0 + h01 * p1 + h11 * dx * m1
}

impl UnifiedCurve {
    pub fn new(
        curve_type: ResponseCurve,
        mut params: CurveParams,
        dead_zone_inner: f32,
        dead_zone_outer: f32,
    ) -> Self {
        params
            .custom_points
            .sort_by(|a, b| a.0.partial_cmp(&b.0).unwrap_or(std::cmp::Ordering::Equal));

        let lut = if curve_type == ResponseCurve::Custom && !params.custom_points.is_empty() {
            let mut table = Box::new([0.0f32; LUT_SIZE]);

            for i in 0..LUT_SIZE {
                let x = i as f32 / (LUT_SIZE - 1) as f32;
                table[i] = Self::interpolate_at_point(
                    &params.custom_points,
                    x,
                    params.use_smooth_interpolation,
                );
            }

            Some(table)
        } else {
            None
        };

        Self {
            curve_type,
            params,
            dead_zone_inner,
            dead_zone_outer,
            lut,
        }
    }

    /// Interpolate at a specific point for LUT generation.
    #[inline]
    fn interpolate_at_point(points: &[(f32, f32)], x: f32, use_smooth: bool) -> f32 {
        if points.is_empty() {
            return x;
        }
        if points.len() == 1 {
            return points[0].1;
        }

        for i in 0..(points.len() - 1) {
            let p1 = points[i];
            let p2 = points[i + 1];

            if x >= p1.0 && x <= p2.0 {
                let dx = p2.0 - p1.0;

                if dx.abs() < 1e-10 {
                    return (p1.1 + p2.1) * 0.5;
                }

                let t = (x - p1.0) / dx;

                if use_smooth {
                    let m1 = if i == 0 {
                        (p2.1 - p1.1) / dx
                    } else {
                        let p0 = points[i - 1];
                        let dx0 = (p1.0 - p0.0).max(1e-10);
                        ((p2.1 - p1.1) / dx + (p1.1 - p0.1) / dx0) * 0.5
                    };

                    let m2 = if i == points.len() - 2 {
                        (p2.1 - p1.1) / dx
                    } else {
                        let p3 = points[i + 2];
                        let dx2 = (p3.0 - p2.0).max(1e-10);
                        ((p3.1 - p2.1) / dx2 + (p2.1 - p1.1) / dx) * 0.5
                    };

                    let y = hermite_interp(t, p1.1, p2.1, m1, m2, dx);

                    return y.clamp(0.0, 1.0);
                } else {
                    return linear_interp(t, p1.1, p2.1 - p1.1);
                }
            }
        }

        if x <= points[0].0 {
            return points[0].1;
        }

        points[points.len() - 1].1
    }

    /// Apply curve transformation to normalized input [0.0, 1.0]
    #[inline(always)]
    fn apply_curve(&self, normalized_input: f32) -> f32 {
        match self.curve_type {
            ResponseCurve::Linear => normalized_input,
            ResponseCurve::Custom => {
                if let Some(ref lut) = self.lut {
                    self.lut_lookup(normalized_input, lut)
                } else {
                    normalized_input
                }
            }
        }
    }

    /// LUT lookup with linear interpolation between entries.
    #[inline(always)]
    fn lut_lookup(&self, x: f32, lut: &[f32; LUT_SIZE]) -> f32 {
        let x = x.clamp(0.0, 1.0);

        let index_f = x * (LUT_SIZE - 1) as f32;
        let index = index_f as usize;

        if index >= LUT_SIZE - 1 {
            return lut[LUT_SIZE - 1];
        }

        let fract = index_f - index as f32;
        let v0 = lut[index];
        let v1 = lut[index + 1];

        v0 + fract * (v1 - v0)
    }
}

impl CurveProcessor for UnifiedCurve {
    /// Apply dead zones and the selected curve to the input value.
    #[inline(always)]
    fn process_input(&self, raw_value: f32) -> f32 {
        if raw_value < self.dead_zone_inner {
            return 0.0;
        }

        let clamped = raw_value.min(self.dead_zone_outer);

        let normalized = if self.dead_zone_outer > self.dead_zone_inner {
            (clamped - self.dead_zone_inner) / (self.dead_zone_outer - self.dead_zone_inner)
        } else {
            clamped
        };

        self.apply_curve(normalized)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_linear_curve() {
        let curve = UnifiedCurve::new(ResponseCurve::Linear, CurveParams::default(), 0.0, 1.0);
        assert_eq!(curve.process_input(0.5), 0.5);
        assert_eq!(curve.process_input(0.0), 0.0);
        assert_eq!(curve.process_input(1.0), 1.0);
    }

    #[test]
    fn test_dead_zones() {
        let curve = UnifiedCurve::new(ResponseCurve::Linear, CurveParams::default(), 0.1, 0.9);
        assert_eq!(curve.process_input(0.05), 0.0);

        // (0.5-0.1)/(0.9-0.1) = 0.4/0.8 = 0.5
        let middle_result = curve.process_input(0.5);
        assert!((middle_result - 0.5).abs() < 0.001);

        assert_eq!(curve.process_input(0.95), 1.0);
    }

    #[test]
    fn test_custom_curve_linear_interpolation() {
        let params = CurveParams {
            use_smooth_interpolation: false,
            custom_points: vec![(0.0, 0.0), (0.5, 0.8), (1.0, 1.0)],
        };
        let curve = UnifiedCurve::new(ResponseCurve::Custom, params, 0.0, 1.0);

        assert!((curve.process_input(0.0) - 0.0).abs() < 0.01);
        assert!((curve.process_input(0.5) - 0.8).abs() < 0.01);
        assert!((curve.process_input(1.0) - 1.0).abs() < 0.01);

        let result = curve.process_input(0.25);
        assert!((result - 0.4).abs() < 0.01);
    }

    #[test]
    fn test_custom_curve_smooth_interpolation() {
        let params = CurveParams {
            use_smooth_interpolation: true,
            custom_points: vec![(0.0, 0.0), (1.0, 1.0)],
        };
        let curve = UnifiedCurve::new(ResponseCurve::Custom, params, 0.0, 1.0);

        let result = curve.process_input(0.5);
        assert!((result - 0.5).abs() < 0.01);

        let result_quarter = curve.process_input(0.25);
        let result_three_quarters = curve.process_input(0.75);

        assert!(result_quarter >= 0.0 && result_quarter <= 1.0);
        assert!(result_three_quarters >= 0.0 && result_three_quarters <= 1.0);
    }
}
