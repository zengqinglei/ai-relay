import { Component, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';

@Component({
  selector: 'app-landing-footer',
  standalone: true,
  imports: [RouterModule],
  templateUrl: './footer.html'
})
export class LandingFooter {
  private router = inject(Router);

  /**
   * 导航到页面指定区域
   */
  navigateToSection(fragment: string): void {
    this.router.navigate(['/landing'], { fragment });
  }
}
