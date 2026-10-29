import { Component, ElementRef, Input, OnChanges, ViewChild } from '@angular/core';
import QRCode from 'qrcode';

/** Renders a QR code as an <img> (prints cleanly on thermal and A4 printers). */
@Component({
  selector: 'app-qr-code',
  standalone: true,
  template: `<img #img [attr.width]="size" [attr.height]="size" [attr.alt]="alt" class="qr">`,
  styles: [`.qr { display: block; image-rendering: pixelated; }`]
})
export class QrCode implements OnChanges {
  @Input({ required: true }) value = '';
  @Input() size = 140;
  @Input() alt = 'رمز QR';
  @ViewChild('img', { static: true }) private img!: ElementRef<HTMLImageElement>;

  async ngOnChanges(): Promise<void> {
    if (!this.value) return;
    this.img.nativeElement.src = await QRCode.toDataURL(this.value, { errorCorrectionLevel: 'M', margin: 1, width: this.size * 2 });
  }
}
