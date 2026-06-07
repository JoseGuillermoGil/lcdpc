import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';

type BranchCard = {
  id: string;
  name: string;
  address: string;
  phone: string;
  icon: string;
};

@Component({
  selector: 'app-branches',
  standalone: true,
  imports: [CommonModule, CardModule, TagModule],
  templateUrl: './branches.component.html',
  styleUrl: './branches.component.scss'
})
export class BranchesComponent {
  @Input() branches: BranchCard[] = [];
}
