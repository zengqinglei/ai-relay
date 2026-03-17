import { Component } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';
import { RippleModule } from 'primeng/ripple';

interface PricingPlan {
  name: string;
  price: number;
  image: string;
  buttonLabel: string;
  features: string[];
}

@Component({
  selector: 'app-landing-pricing',
  standalone: true,
  imports: [DividerModule, ButtonModule, RippleModule],
  templateUrl: './pricing.html'
})
export class LandingPricing {
  pricingPlans: PricingPlan[] = [
    {
      name: 'Free',
      price: 0,
      image: 'https://primefaces.org/cdn/templates/sakai/landing/free.svg',
      buttonLabel: 'Get Started',
      features: ['Responsive Layout', 'Unlimited Push Messages', '50 Support Ticket', 'Free Shipping']
    },
    {
      name: 'Startup',
      price: 1,
      image: 'https://primefaces.org/cdn/templates/sakai/landing/startup.svg',
      buttonLabel: 'Get Started',
      features: ['Responsive Layout', 'Unlimited Push Messages', '50 Support Ticket', 'Free Shipping']
    },
    {
      name: 'Enterprise',
      price: 5,
      image: 'https://primefaces.org/cdn/templates/sakai/landing/enterprise.svg',
      buttonLabel: 'Try Free',
      features: ['Responsive Layout', 'Unlimited Push Messages', '50 Support Ticket', 'Free Shipping']
    }
  ];
}
