import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';
import { RippleModule } from 'primeng/ripple';
import { StyleClassModule } from 'primeng/styleclass';

// Landing组件导入
import { LandingFeatures } from '../../widgets/features/features';
import { LandingFooter } from '../../widgets/footer/footer';
import { LandingHeader } from '../../widgets/header/header';
import { LandingHero } from '../../widgets/hero/hero';
import { LandingHighlights } from '../../widgets/highlights/highlights';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [
    RouterModule,
    RippleModule,
    StyleClassModule,
    ButtonModule,
    DividerModule,
    LandingHeader,
    LandingHero,
    LandingFeatures,
    LandingHighlights,
    LandingFooter
  ],
  templateUrl: './landing.html'
})
export class Landing {}
