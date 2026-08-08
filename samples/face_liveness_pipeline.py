"""DryCar yüz akışının sadeleştirilmiş, bağımsız portföy örneği."""

from collections.abc import Sequence
from math import dist, sqrt


def eye_aspect_ratio(points: Sequence[tuple[float, float]]) -> float:
    """Altı göz işaretinden gözün açıklık oranını hesaplar."""
    if len(points) != 6:
        raise ValueError("EAR hesabı tam olarak altı nokta bekler")

    vertical = dist(points[1], points[5]) + dist(points[2], points[4])
    horizontal = 2 * dist(points[0], points[3])
    return vertical / horizontal if horizontal else 0.0


def contains_blink(
    ear_samples: Sequence[float],
    *,
    open_threshold: float = 0.24,
    closed_threshold: float = 0.18,
) -> bool:
    """Açık gözden kapalı göze geçiş olup olmadığını kontrol eder."""
    saw_open_eyes = False

    for value in ear_samples:
        if value >= open_threshold:
            saw_open_eyes = True
        elif saw_open_eyes and value <= closed_threshold:
            return True

    return False


def vector_distance(saved: Sequence[float], current: Sequence[float]) -> float:
    """İki yüz tanımlayıcısı arasındaki Öklid mesafesini döndürür."""
    if len(saved) != len(current) or not saved:
        raise ValueError("Yüz vektörlerinin boyutları eşit ve dolu olmalıdır")

    return sqrt(sum((left - right) ** 2 for left, right in zip(saved, current)))


def verify_identity(
    saved_vector: Sequence[float],
    current_vector: Sequence[float],
    ear_samples: Sequence[float],
    *,
    match_threshold: float = 0.6,
) -> bool:
    """Canlılık ve yüz benzerliği birlikte başarılıysa kimliği doğrular."""
    return contains_blink(ear_samples) and vector_distance(
        saved_vector, current_vector
    ) < match_threshold

