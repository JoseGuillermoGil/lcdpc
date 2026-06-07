import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';

export type SearchResultItem = {
  id: string;
  name: string;
  description: string;
  price: string;
  unitLabel: string;
  imageUrl: string;
  alt: string;
  tags: string[];
  featured?: boolean;
  promo?: string;
  oldPrice?: string;
  soldOut?: boolean;
};

@Component({
  selector: 'app-advanced-search',
  standalone: true,
  imports: [CommonModule, ButtonModule, CardModule],
  templateUrl: './advanced-search.component.html',
  styleUrl: './advanced-search.component.scss'
})
export class AdvancedSearchComponent {
  @Input() query = 'salchichas premium';
  @Input() results: SearchResultItem[] = [];
}
