import { Component } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CommonModule } from '@angular/common';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  exact?: boolean;
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './layout.component.html',
  styleUrls: ['./layout.component.css']
})
export class LayoutComponent {
  user: any;
  now = new Date();

  nav: NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dash', exact: true },
    { path: '/pos', label: 'Point of Sale', icon: 'pos' },
    { path: '/kitchen', label: 'Kitchen (KDS)', icon: 'kitchen' },
    { path: '/tables', label: 'Tables & Floor', icon: 'tables' },
    { path: '/inventory', label: 'Inventory', icon: 'inventory' },
    { path: '/menu', label: 'Menu & Catalogue', icon: 'menu' },
    { path: '/reports', label: 'Reports', icon: 'reports' },
    { path: '/staff', label: 'Staff', icon: 'staff' },
  ];

  constructor(private authService: AuthService, private router: Router) {
    this.user = this.authService.getCurrentUser();
  }

  get initials(): string {
    const n = this.user?.username || 'U';
    return n.substring(0, 2).toUpperCase();
  }

  logout() {
    if (!confirm('Sign out of ORO POS?')) return;
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
