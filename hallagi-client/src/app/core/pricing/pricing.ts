/**
 * Client-side mirror of the server pricing rules (Hallagi.Application.Tax.Pricing) used for
 * the live POS preview. The server recalculates everything — this is display only.
 */
export interface PriceBreakdown { gross: number; discount: number; subtotal: number; vat: number; total: number; }

export const round2 = (v: number): number => Math.round((v + Number.EPSILON) * 100) / 100;

export function calculatePrice(gross: number, discount: number, vatEnabled: boolean, vatRate: number, pricesIncludeVat: boolean): PriceBreakdown {
  gross = round2(gross);
  discount = round2(Math.min(Math.max(discount, 0), gross));
  const net = gross - discount;
  if (!vatEnabled || vatRate <= 0) return { gross, discount, subtotal: net, vat: 0, total: net };
  if (pricesIncludeVat) {
    const subtotal = round2(net / (1 + vatRate));
    return { gross, discount, subtotal, vat: round2(net - subtotal), total: net };
  }
  const vat = round2(net * vatRate);
  return { gross, discount, subtotal: net, vat, total: round2(net + vat) };
}
