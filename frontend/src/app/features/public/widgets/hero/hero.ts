import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { RippleModule } from 'primeng/ripple';

@Component({
  selector: 'app-landing-hero',
  standalone: true,
  imports: [ButtonModule, RippleModule, RouterModule],
  templateUrl: './hero.html'
})
export class LandingHero {}
