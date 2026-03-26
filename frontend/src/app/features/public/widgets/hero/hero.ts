import { Component } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { RippleModule } from 'primeng/ripple';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-landing-hero',
  standalone: true,
  imports: [ButtonModule, RippleModule, RouterModule],
  templateUrl: './hero.html'
})
export class LandingHero {}
