import { Component } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { RippleModule } from 'primeng/ripple';

@Component({
  selector: 'app-landing-hero',
  standalone: true,
  imports: [ButtonModule, RippleModule],
  templateUrl: './hero.html'
})
export class LandingHero {}
