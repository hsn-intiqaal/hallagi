import { calculatePrice, round2 } from './pricing';

describe('calculatePrice (mirror of the server pricing rules)', () => {
  it('extracts 15% VAT from VAT-inclusive prices', () => {
    expect(calculatePrice(115, 0, true, 0.15, true)).toEqual({ gross: 115, discount: 0, subtotal: 100, vat: 15, total: 115 });
  });

  it('adds 15% VAT on top of VAT-exclusive prices', () => {
    expect(calculatePrice(100, 0, true, 0.15, false)).toEqual({ gross: 100, discount: 0, subtotal: 100, vat: 15, total: 115 });
  });

  it('applies the discount before VAT', () => {
    const p = calculatePrice(100, 20, true, 0.15, false);
    expect(p.subtotal).toBe(80);
    expect(p.vat).toBe(12);
    expect(p.total).toBe(92);
  });

  it('never discounts below zero or above the gross amount', () => {
    expect(calculatePrice(50, 80, true, 0.15, true).total).toBe(0);
    expect(calculatePrice(50, -5, true, 0.15, true).discount).toBe(0);
  });

  it('charges no VAT when VAT is disabled', () => {
    expect(calculatePrice(35, 0, false, 0.15, true)).toEqual({ gross: 35, discount: 0, subtotal: 35, vat: 0, total: 35 });
  });

  it('keeps subtotal + vat equal to the total for awkward amounts', () => {
    for (const gross of [0.01, 33.33, 35, 99.99, 1234.56]) {
      const p = calculatePrice(gross, 0, true, 0.15, true);
      expect(round2(p.subtotal + p.vat)).toBe(p.total);
    }
  });
});
