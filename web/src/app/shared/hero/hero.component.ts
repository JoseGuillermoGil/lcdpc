import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { CarouselModule } from 'primeng/carousel';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { InputTextModule } from 'primeng/inputtext';
import { CarouselPageEvent } from 'primeng/types/carousel';

type HeroSlide = {
  title: string;
  imageUrl: string;
  alt: string;
};

@Component({
  selector: 'app-hero',
  standalone: true,
  imports: [CommonModule, ButtonModule, CarouselModule, InputGroupModule, InputGroupAddonModule, InputTextModule],
  templateUrl: './hero.component.html',
  styleUrl: './hero.component.scss'
})
export class HeroComponent {
  @Input() heroSlides: HeroSlide[] = [];
  @Input() activeHeroIndex = 0;
  @Input() search = '';

  @Output() previous = new EventEmitter<void>();
  @Output() next = new EventEmitter<void>();
  @Output() slideSelect = new EventEmitter<number>();
  @Output() searchChange = new EventEmitter<string>();

  protected onCarouselPage(event: CarouselPageEvent): void {
    this.slideSelect.emit(event.page ?? 0);
  }

  protected selectSlide(index: number): void {
    this.slideSelect.emit(index);
  }
}
